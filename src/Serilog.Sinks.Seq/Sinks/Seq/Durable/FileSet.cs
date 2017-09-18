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

#if DURABLE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Serilog.Debugging;

namespace Serilog.Sinks.Seq.Durable
{
    class FileSet
    {
        readonly string _bookmarkFilename;
        readonly string _candidateSearchPath;
        readonly string _logFolder;

        const string InvalidPayloadFilePrefix = "invalid-";

        public FileSet(string bufferBaseFilename)
        {
            if (bufferBaseFilename == null) throw new ArgumentNullException(nameof(bufferBaseFilename));

            _bookmarkFilename = Path.GetFullPath(bufferBaseFilename + ".bookmark");
            _logFolder = Path.GetDirectoryName(_bookmarkFilename);
            _candidateSearchPath = Path.GetFileName(bufferBaseFilename) + "*.json";
        }

        public BookmarkFile OpenBookmarkFile()
        {
            return new BookmarkFile(_bookmarkFilename);
        }

        public string MakeInvalidPayloadFilename(HttpStatusCode statusCode)
        {
            var invalidPayloadFilename = $"{InvalidPayloadFilePrefix}{statusCode}-{Guid.NewGuid():n}.json";
            return Path.Combine(_logFolder, invalidPayloadFilename);
        }

        public void CleanUpInvalidPayloadFiles(long maxNumberOfBytesToRetain)
        {
            try
            {
                var candiateFiles = Directory.EnumerateFiles(_logFolder, $"{InvalidPayloadFilePrefix}*.json");
                DeleteOldFiles(maxNumberOfBytesToRetain, candiateFiles);
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("Exception thrown while trying to clean up invalid payload files: {0}", ex);
            }
        }

        public string[] GetFiles()
        {
            return Directory.GetFiles(_logFolder, _candidateSearchPath)
                .OrderBy(n => n)
                .ToArray();
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

    }
}

#endif
