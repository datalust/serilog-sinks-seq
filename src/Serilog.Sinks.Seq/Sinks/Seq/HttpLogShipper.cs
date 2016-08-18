// Serilog.Sinks.Seq Copyright 2016 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#if DURABLE

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using IOFile = System.IO.File;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Serilog.Sinks.Seq
{
    class HttpLogShipper : IDisposable
    {
        static readonly TimeSpan RequiredLevelCheckInterval = TimeSpan.FromMinutes(2);

        readonly string _apiKey;
        readonly int _batchPostingLimit;
        readonly long? _eventBodyLimitBytes;
        readonly string _bookmarkFilename;
        readonly string _logFolder;
        readonly HttpClient _httpClient;
        readonly string _candidateSearchPath;
        readonly ExponentialBackoffConnectionSchedule _connectionSchedule;
        readonly long? _retainedInvalidPayloadsLimitBytes;

        readonly object _stateLock = new object();

#if !WAITABLE_TIMER
        readonly PortableTimer _timer;
#else
        readonly Timer _timer;
#endif

        LoggingLevelSwitch _levelControlSwitch;
        DateTime _nextRequiredLevelCheckUtc = DateTime.UtcNow.Add(RequiredLevelCheckInterval);
        volatile bool _unloading;

        public HttpLogShipper(
            string serverUrl,
            string bufferBaseFilename,
            string apiKey,
            int batchPostingLimit,
            TimeSpan period,
            long? eventBodyLimitBytes,
            LoggingLevelSwitch levelControlSwitch,
            HttpMessageHandler messageHandler,
            long? retainedInvalidPayloadsLimitBytes)
        {
            _apiKey = apiKey;
            _batchPostingLimit = batchPostingLimit;
            _eventBodyLimitBytes = eventBodyLimitBytes;
            _levelControlSwitch = levelControlSwitch;
            _connectionSchedule = new ExponentialBackoffConnectionSchedule(period);
            _retainedInvalidPayloadsLimitBytes = retainedInvalidPayloadsLimitBytes;
            _httpClient = messageHandler != null ?
                new HttpClient(messageHandler) :
                new HttpClient();
            _httpClient.BaseAddress = new Uri(SeqApi.NormalizeServerBaseAddress(serverUrl));

            _bookmarkFilename = Path.GetFullPath(bufferBaseFilename + ".bookmark");
            _logFolder = Path.GetDirectoryName(_bookmarkFilename);
            _candidateSearchPath = Path.GetFileName(bufferBaseFilename) + "*.json";

#if !WAITABLE_TIMER
            _timer = new PortableTimer(c => OnTick());
#else
            _timer = new Timer(s => OnTick());
#endif

            SetTimer();
        }

        void CloseAndFlush()
        {
            lock (_stateLock)
            {
                if (_unloading)
                    return;

                _unloading = true;
            }
#if !WAITABLE_TIMER
            _timer.Dispose();
#else
            var wh = new ManualResetEvent(false);
            if (_timer.Dispose(wh))
                wh.WaitOne();
#endif

            OnTick();
        }

        /// <summary>
        /// Get the last "minimum level" indicated by the Seq server, if any.
        /// </summary>
        public LogEventLevel? MinimumAcceptedLevel
        {
            get
            {
                lock (_stateLock)
                    return _levelControlSwitch?.MinimumLevel;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Free resources held by the sink.
        /// </summary>
        /// <param name="disposing">If true, called because the object is being disposed; if false,
        /// the object is being disposed from the finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;
            CloseAndFlush();
        }

        void SetTimer()
        {
            // Note, called under _stateLock

#if !WAITABLE_TIMER
            _timer.Start(_connectionSchedule.NextInterval);
#else
            _timer.Change(_connectionSchedule.NextInterval, Timeout.InfiniteTimeSpan);
#endif
        }

        void OnTick()
        {
            LogEventLevel? minimumAcceptedLevel = null;

            try
            {
                int count;
                do
                {
                    count = 0;

                    // Locking the bookmark ensures that though there may be multiple instances of this
                    // class running, only one will ship logs at a time.

                    using (var bookmark = IOFile.Open(_bookmarkFilename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                    {
                        long nextLineBeginsAtOffset;
                        string currentFile;

                        TryReadBookmark(bookmark, out nextLineBeginsAtOffset, out currentFile);

                        var fileSet = GetFileSet();

                        if (currentFile == null || !IOFile.Exists(currentFile))
                        {
                            nextLineBeginsAtOffset = 0;
                            currentFile = fileSet.FirstOrDefault();
                        }

                        if (currentFile == null)
                            continue;

                        var payload = ReadPayload(currentFile, ref nextLineBeginsAtOffset, ref count);

                        if (count > 0 || _levelControlSwitch != null && _nextRequiredLevelCheckUtc < DateTime.UtcNow)
                        {
                            lock (_stateLock)
                            {
                                _nextRequiredLevelCheckUtc = DateTime.UtcNow.Add(RequiredLevelCheckInterval);
                            }

                            var content = new StringContent(payload, Encoding.UTF8, "application/json");
                            if (!string.IsNullOrWhiteSpace(_apiKey))
                                content.Headers.Add(SeqApi.ApiKeyHeaderName, _apiKey);

                            var result = _httpClient.PostAsync(SeqApi.BulkUploadResource, content).Result;
                            if (result.IsSuccessStatusCode)
                            {
                                _connectionSchedule.MarkSuccess();
                                WriteBookmark(bookmark, nextLineBeginsAtOffset, currentFile);
                                var returned = result.Content.ReadAsStringAsync().Result;
                                minimumAcceptedLevel = SeqApi.ReadEventInputResult(returned);
                            }
                            else if (result.StatusCode == HttpStatusCode.BadRequest ||
                                     result.StatusCode == HttpStatusCode.RequestEntityTooLarge)
                            {
                                // The connection attempt was successful - the payload we sent was the problem.
                                _connectionSchedule.MarkSuccess();

                                DumpInvalidPayload(result, payload).Wait();

                                WriteBookmark(bookmark, nextLineBeginsAtOffset, currentFile);
                            }
                            else
                            {
                                _connectionSchedule.MarkFailure();
                                SelfLog.WriteLine("Received failed HTTP shipping result {0}: {1}", result.StatusCode, result.Content.ReadAsStringAsync().Result);
                                break;
                            }
                        }
                        else
                        {
                            // For whatever reason, there's nothing waiting to send. This means we should try connecting again at the
                            // regular interval, so mark the attempt as successful.
                            _connectionSchedule.MarkSuccess();

                            // Only advance the bookmark if no other process has the
                            // current file locked, and its length is as we found it.

                            if (fileSet.Length == 2 && fileSet.First() == currentFile && IsUnlockedAtLength(currentFile, nextLineBeginsAtOffset))
                            {
                                WriteBookmark(bookmark, 0, fileSet[1]);
                            }

                            if (fileSet.Length > 2)
                            {
                                // Once there's a third file waiting to ship, we do our
                                // best to move on, though a lock on the current file
                                // will delay this.

                                IOFile.Delete(fileSet[0]);
                            }
                        }
                    }
                }
                while (count == _batchPostingLimit);
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("Exception while emitting periodic batch from {0}: {1}", this, ex);
                _connectionSchedule.MarkFailure();
            }
            finally
            {
                lock (_stateLock)
                {
                    UpdateLevelControlSwitch(minimumAcceptedLevel);

                    if (!_unloading)
                        SetTimer();
                }
            }
        }

        const string InvalidPayloadFilePrefix = "invalid-";
        async Task DumpInvalidPayload(HttpResponseMessage result, string payload)
        {
            var invalidPayloadFilename = $"{InvalidPayloadFilePrefix}{result.StatusCode}-{Guid.NewGuid():n}.json";
            var invalidPayloadFile = Path.Combine(_logFolder, invalidPayloadFilename);
            var resultContent = await result.Content.ReadAsStringAsync();
            SelfLog.WriteLine("HTTP shipping failed with {0}: {1}; dumping payload to {2}", result.StatusCode, resultContent, invalidPayloadFile);
            var bytesToWrite = Encoding.UTF8.GetBytes(payload);
            if (_retainedInvalidPayloadsLimitBytes.HasValue)
            {
                CleanUpInvalidPayloadFiles(_retainedInvalidPayloadsLimitBytes.Value - bytesToWrite.Length, _logFolder);
            }
            IOFile.WriteAllBytes(invalidPayloadFile, bytesToWrite);
        }

        static void CleanUpInvalidPayloadFiles(long maxNumberOfBytesToRetain, string logFolder)
        {
            try
            {
                var candiateFiles = Directory.EnumerateFiles(logFolder, $"{InvalidPayloadFilePrefix}*.json");
                DeleteOldFiles(maxNumberOfBytesToRetain, candiateFiles);
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("Exception thrown while trying to clean up invalid payload files: {0}", ex);
            }
        }

        static IEnumerable<FileInfo> WhereCumulativeSizeGreaterThan(IEnumerable<FileInfo> files, long maxCumulativeSize)
        {
            long cumulative = 0;
            foreach (var file in files)
            {
                cumulative += file.Length;
                if (cumulative > maxCumulativeSize)
                {
                    yield return file;
                }
            }
        }

        static void DeleteOldFiles(long maxNumberOfBytesToRetain, IEnumerable<string> files)
        {
            var orderedFileInfos = from candiateFile in files
                                   let candiateFileInfo = new FileInfo(candiateFile)
                                   orderby candiateFileInfo.LastAccessTimeUtc descending
                                   select candiateFileInfo;

            var invalidPayloadFilesToDelete = WhereCumulativeSizeGreaterThan(orderedFileInfos, maxNumberOfBytesToRetain);

            foreach (var fileToDelete in invalidPayloadFilesToDelete)
            {
                try
                {
                    fileToDelete.Delete();
                }
                catch (Exception ex)
                {
                    SelfLog.WriteLine("Exception '{0}' thrown while trying to delete file {1}", ex.Message, fileToDelete.FullName);
                }
            }
        }

        void UpdateLevelControlSwitch(LogEventLevel? minimumAcceptedLevel)
        {
            if (minimumAcceptedLevel == null)
            {
                if (_levelControlSwitch != null)
                    _levelControlSwitch.MinimumLevel = LevelAlias.Minimum;
            }
            else
            {
                if (_levelControlSwitch == null)
                    _levelControlSwitch = new LoggingLevelSwitch(minimumAcceptedLevel.Value);
                else
                    _levelControlSwitch.MinimumLevel = minimumAcceptedLevel.Value;
            }
        }

        string ReadPayload(string currentFile, ref long nextLineBeginsAtOffset, ref int count)
        {
            var payload = new StringWriter();
            payload.Write("{\"Events\":[");
            var delimStart = "";

            using (var current = IOFile.Open(currentFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                current.Position = nextLineBeginsAtOffset;

                string nextLine;
                while (count < _batchPostingLimit &&
                       TryReadLine(current, ref nextLineBeginsAtOffset, out nextLine))
                {
                    // Count is the indicator that work was done, so advances even in the (rare) case an
                    // oversized event is dropped.
                    ++count;

                    if (_eventBodyLimitBytes.HasValue && Encoding.UTF8.GetByteCount(nextLine) > _eventBodyLimitBytes.Value)
                    {
                        SelfLog.WriteLine(
                            "Event JSON representation exceeds the byte size limit of {0} and will be dropped; data: {1}",
                            _eventBodyLimitBytes, nextLine);
                    }
                    else
                    {
                        payload.Write(delimStart);
                        payload.Write(nextLine);
                        delimStart = ",";
                    }
                }

                payload.Write("]}");
            }
            return payload.ToString();
        }

        static bool IsUnlockedAtLength(string file, long maxLen)
        {
            try
            {
                using (var fileStream = IOFile.Open(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                {
                    return fileStream.Length <= maxLen;
                }
            }
            catch (IOException ex)
            {
                var errorCode = Marshal.GetHRForException(ex) & ((1 << 16) - 1);
                if (errorCode != 32 && errorCode != 33)
                {
                    SelfLog.WriteLine("Unexpected I/O exception while testing locked status of {0}: {1}", file, ex);
                }
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("Unexpected exception while testing locked status of {0}: {1}", file, ex);
            }

            return false;
        }

        static void WriteBookmark(FileStream bookmark, long nextLineBeginsAtOffset, string currentFile)
        {
            using (var writer = new StreamWriter(bookmark))
            {
                writer.WriteLine("{0}:::{1}", nextLineBeginsAtOffset, currentFile);
            }
        }

        // It would be ideal to chomp whitespace here, but not required.
        static bool TryReadLine(Stream current, ref long nextStart, out string nextLine)
        {
            var includesBom = nextStart == 0;

            if (current.Length <= nextStart)
            {
                nextLine = null;
                return false;
            }

            current.Position = nextStart;

            // Important not to dispose this StreamReader as the stream must remain open.
            var reader = new StreamReader(current, Encoding.UTF8, false, 128);
            nextLine = reader.ReadLine();

            if (nextLine == null)
                return false;

            nextStart += Encoding.UTF8.GetByteCount(nextLine) + Encoding.UTF8.GetByteCount(Environment.NewLine);
            if (includesBom)
                nextStart += 3;

            return true;
        }

        static void TryReadBookmark(Stream bookmark, out long nextLineBeginsAtOffset, out string currentFile)
        {
            nextLineBeginsAtOffset = 0;
            currentFile = null;

            if (bookmark.Length != 0)
            {
                // Important not to dispose this StreamReader as the stream must remain open.
                var reader = new StreamReader(bookmark, Encoding.UTF8, false, 128);
                var current = reader.ReadLine();

                if (current != null)
                {
                    bookmark.Position = 0;
                    var parts = current.Split(new[] { ":::" }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        nextLineBeginsAtOffset = long.Parse(parts[0]);
                        currentFile = parts[1];
                    }
                }

            }
        }

        string[] GetFileSet()
        {
            return Directory.GetFiles(_logFolder, _candidateSearchPath)
                .OrderBy(n => n)
                .ToArray();
        }
    }
}

#endif
