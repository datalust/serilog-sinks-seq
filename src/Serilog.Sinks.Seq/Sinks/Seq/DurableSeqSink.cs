﻿// Serilog.Sinks.Seq Copyright 2016 Serilog Contributors
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
using Serilog.Sinks.RollingFile;
using System.Net.Http;
using System.Text;

namespace Serilog.Sinks.Seq
{
    class DurableSeqSink : ILogEventSink, IDisposable
    {
        readonly HttpLogShipper _shipper;
        readonly RollingFileSink _sink;

        public DurableSeqSink(
            string serverUrl,
            string bufferBaseFilename,
            string apiKey,
            int batchPostingLimit,
            TimeSpan period,
            long? bufferFileSizeLimitBytes,
            long? eventBodyLimitBytes,
            LoggingLevelSwitch levelControlSwitch,
            HttpMessageHandler messageHandler,
            long? retainedInvalidPayloadsLimitBytes)
        {
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));
            if (bufferBaseFilename == null) throw new ArgumentNullException(nameof(bufferBaseFilename));

            _shipper = new HttpLogShipper(
                serverUrl, 
                bufferBaseFilename, 
                apiKey, 
                batchPostingLimit, 
                period, 
                eventBodyLimitBytes, 
                levelControlSwitch,
                messageHandler,
                retainedInvalidPayloadsLimitBytes);

            _sink = new RollingFileSink(
                bufferBaseFilename + "-{Date}.json",
                new RawJsonFormatter(),
                bufferFileSizeLimitBytes,
                null,
                encoding: Encoding.UTF8);
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
            var minimumAcceptedLevel = _shipper.MinimumAcceptedLevel;
            if (minimumAcceptedLevel == null ||
                (int)minimumAcceptedLevel <= (int)logEvent.Level)
            {
                _sink.Emit(logEvent);
            }
        }
    }
}

#endif
