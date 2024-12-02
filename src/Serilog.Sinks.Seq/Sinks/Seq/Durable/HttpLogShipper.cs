// Serilog.Sinks.Seq Copyright 2017 Serilog Contributors
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

using System.Net;
using System.Text;
using Serilog.Debugging;
using Serilog.Events;
using IOFile = System.IO.File;
using Serilog.Sinks.Seq.Http;

#if HRESULTS
using System.Runtime.InteropServices;
#endif

namespace Serilog.Sinks.Seq.Durable;

sealed class HttpLogShipper : IDisposable
#if ASYNC_DISPOSE
        , IAsyncDisposable
#endif
{
    static readonly TimeSpan RequiredLevelCheckInterval = TimeSpan.FromMinutes(2);

    readonly int _batchPostingLimit;
    readonly long? _eventBodyLimitBytes;
    readonly FileSet _fileSet;
    readonly long? _retainedInvalidPayloadsLimitBytes;
    readonly long? _bufferSizeLimitBytes;

    // Timer thread only
    readonly SeqIngestionApi _ingestionApi;

    readonly ExponentialBackoffConnectionSchedule _connectionSchedule;
    DateTime _nextRequiredLevelCheckUtc = DateTime.UtcNow.Add(RequiredLevelCheckInterval);

    // Synchronized
    readonly object _stateLock = new();

    readonly PortableTimer _timer;

    // Concurrent
    readonly ControlledLevelSwitch _controlledSwitch;

    volatile bool _unloading;

    public HttpLogShipper(
        FileSet fileSet,
        SeqIngestionApi ingestionApi,
        int batchPostingLimit,
        TimeSpan period,
        long? eventBodyLimitBytes,
        ControlledLevelSwitch controlledSwitch,
        long? retainedInvalidPayloadsLimitBytes,
        long? bufferSizeLimitBytes)
    {
        _fileSet = fileSet ?? throw new ArgumentNullException(nameof(fileSet));
        _ingestionApi = ingestionApi ?? throw new ArgumentNullException(nameof(ingestionApi));
        _batchPostingLimit = batchPostingLimit;
        _eventBodyLimitBytes = eventBodyLimitBytes;
        _controlledSwitch = controlledSwitch;
        _connectionSchedule = new ExponentialBackoffConnectionSchedule(period);
        _retainedInvalidPayloadsLimitBytes = retainedInvalidPayloadsLimitBytes;
        _bufferSizeLimitBytes = bufferSizeLimitBytes;
        _timer = new PortableTimer(_ => OnTick());

        SetTimer();
    }

    public bool IsIncluded(LogEvent logEvent)
    {
        return _controlledSwitch.IsIncluded(logEvent);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_unloading)
                return;

            _unloading = true;
        }

        _timer.Dispose();

        Task.Run(OnTick).Wait();
    }
        
#if ASYNC_DISPOSE
        public async ValueTask DisposeAsync()
        {
            lock (_stateLock)
            {
                if (_unloading)
                    return;

                _unloading = true;
            }

            _timer.Dispose();

            await OnTick().ConfigureAwait(false);
        }
#endif
        
    void SetTimer()
    {
        // Note, called under _stateLock
        _timer.Start(_connectionSchedule.NextInterval);
    }

    async Task OnTick()
    {
        try
        {
            int count;
            do
            {
                count = 0;

                using var bookmarkFile = _fileSet.OpenBookmarkFile();
                var position = bookmarkFile.TryReadBookmark();
                var files = _fileSet.GetBufferFiles();

                if (position.File == null || !IOFile.Exists(position.File))
                {
                    position = new FileSetPosition(0, files.FirstOrDefault());
                }

                string payload, mimeType;
                if (position.File == null)
                {
                    payload = PayloadReader.MakeEmptyPayload(out mimeType);
                    count = 0;
                }
                else
                {
                    payload = PayloadReader.ReadPayload(_batchPostingLimit, _eventBodyLimitBytes, ref position, ref count, out mimeType);
                }

                if (count > 0 || _controlledSwitch.IsActive && _nextRequiredLevelCheckUtc < DateTime.UtcNow)
                {
                    _nextRequiredLevelCheckUtc = DateTime.UtcNow.Add(RequiredLevelCheckInterval);

                    var result = await _ingestionApi.TryIngestAsync(payload, mimeType);
                    if (result.Succeeded)
                    {
                        _connectionSchedule.MarkSuccess();
                        bookmarkFile.WriteBookmark(position);
                        _controlledSwitch.Update(result.MinimumAcceptedLevel);
                    }
                    else if (result.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge)
                    {
                        // The connection attempt was successful - the payload we sent was the problem.
                        _connectionSchedule.MarkSuccess();

                        await DumpInvalidPayloadAsync(result.StatusCode, payload).ConfigureAwait(false);

                        bookmarkFile.WriteBookmark(position);
                    }
                    else
                    {
                        _connectionSchedule.MarkFailure();
                        SelfLog.WriteLine("Received failed HTTP shipping result {0}", result.StatusCode);

                        if (_bufferSizeLimitBytes.HasValue)
                            _fileSet.CleanUpBufferFiles(_bufferSizeLimitBytes.Value);

                        break;
                    }
                }
                else if (position.File == null)
                {
                    break;
                }
                else
                {
                    // For whatever reason, there's nothing waiting to send. This means we should try connecting again at the
                    // regular interval, so mark the attempt as successful.
                    _connectionSchedule.MarkSuccess();

                    // Only advance the bookmark if no other process has the
                    // current file locked, and its length is as we found it.
                    if (files.Length == 2 && files.First() == position.File &&
                        FileIsUnlockedAndUnextended(position))
                    {
                        bookmarkFile.WriteBookmark(new FileSetPosition(0, files[1]));
                    }

                    if (files.Length > 2)
                    {
                        // By this point, we expect writers to have relinquished locks
                        // on the oldest file.
                        IOFile.Delete(files[0]);
                    }
                }
            } while (count == _batchPostingLimit);
        }
        catch (Exception ex)
        {
            _connectionSchedule.MarkFailure();
            SelfLog.WriteLine("Exception while emitting periodic batch from {0}: {1}", this, ex);

            if (_bufferSizeLimitBytes.HasValue)
                _fileSet.CleanUpBufferFiles(_bufferSizeLimitBytes.Value);
        }
        finally
        {
            lock (_stateLock)
            {
                if (!_unloading)
                    SetTimer();
            }
        }
    }

    Task DumpInvalidPayloadAsync(HttpStatusCode statusCode, string payload)
    {
        var invalidPayloadFile = _fileSet.MakeInvalidPayloadFilename(statusCode);
        SelfLog.WriteLine("HTTP shipping failed with {0}; dumping payload to {1}", statusCode, invalidPayloadFile);
        var bytesToWrite = Encoding.UTF8.GetBytes(payload);
        if (_retainedInvalidPayloadsLimitBytes.HasValue)
        {
            _fileSet.CleanUpInvalidPayloadFiles(_retainedInvalidPayloadsLimitBytes.Value - bytesToWrite.Length);
        }
#if WRITE_ALL_BYTES_ASYNC
            return IOFile.WriteAllBytesAsync(invalidPayloadFile, bytesToWrite);
#else
        IOFile.WriteAllBytes(invalidPayloadFile, bytesToWrite);
        return Task.FromResult(0);
#endif
    }

    static bool FileIsUnlockedAndUnextended(FileSetPosition position)
    {
        try
        {
            using var fileStream = IOFile.Open(position.File!, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            return fileStream.Length <= position.NextLineStart;
        }
#if HRESULTS
            catch (IOException ex)
            {
                var errorCode = Marshal.GetHRForException(ex) & ((1 << 16) - 1);
                if (errorCode != 32 && errorCode != 33)
                {
                    SelfLog.WriteLine("Unexpected I/O exception while testing locked status of {0}: {1}", position.File, ex);
                }
            }
#else
        catch (IOException)
        {
            // Where no HRESULT is available, assume IOExceptions indicate a locked file
        }
#endif
        catch (Exception ex)
        {
            SelfLog.WriteLine("Unexpected exception while testing locked status of {0}: {1}", position.File, ex);
        }

        return false;
    }
}