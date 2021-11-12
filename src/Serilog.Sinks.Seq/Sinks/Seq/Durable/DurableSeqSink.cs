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
using Serilog.Core;
using Serilog.Events;
using System.Net.Http;
using System.Text;
using Serilog.Sinks.Seq.Http;

namespace Serilog.Sinks.Seq.Durable
{
    sealed class DurableSeqSink : ILogEventSink, IDisposable
    {
        readonly HttpLogShipper _shipper;
        readonly Logger _sink;

        public DurableSeqSink(
            string serverUrl,
            string bufferBaseFilename,
            string? apiKey,
            int batchPostingLimit,
            TimeSpan period,
            long? bufferSizeLimitBytes,
            long? eventBodyLimitBytes,
            ControlledLevelSwitch controlledSwitch,
            HttpMessageHandler? messageHandler,
            long? retainedInvalidPayloadsLimitBytes)
        {
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));
            if (bufferBaseFilename == null) throw new ArgumentNullException(nameof(bufferBaseFilename));

            var fileSet = new FileSet(bufferBaseFilename);

            _shipper = new HttpLogShipper(
                fileSet,
                new SeqIngestionApiClient(serverUrl, apiKey, messageHandler),
                batchPostingLimit, 
                period, 
                eventBodyLimitBytes,
                controlledSwitch,
                retainedInvalidPayloadsLimitBytes,
                bufferSizeLimitBytes);

            const long individualFileSizeLimitBytes = 100L * 1024 * 1024;
            _sink = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(new ConstrainedBufferedFormatter(eventBodyLimitBytes),
                        fileSet.RollingFilePathFormat,
                        rollingInterval: RollingInterval.Day,
                        fileSizeLimitBytes: individualFileSizeLimitBytes,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: null,
                        encoding: Encoding.UTF8)
                .CreateLogger();
        }

        public void Dispose()
        {
            _sink.Dispose();
            _shipper.Dispose();
        }

        public void Emit(LogEvent logEvent)
        {
            // This is a lagging indicator, but the network bandwidth usage benefits
            // are worth the ambiguity.
            if (_shipper.IsIncluded(logEvent))
            {
                _sink.Write(logEvent);
            }
        }
    }
}

#endif
