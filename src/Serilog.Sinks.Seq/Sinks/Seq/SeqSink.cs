// Serilog.Sinks.Seq Copyright 2016 Serilog Contributors
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Json;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.Seq
{
    class SeqSink : PeriodicBatchingSink
    {
        public const int DefaultBatchPostingLimit = 1000;
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(2);
        public const int DefaultQueueSizeLimit = 100000;

        static readonly TimeSpan RequiredLevelCheckInterval = TimeSpan.FromMinutes(2);
        static readonly JsonValueFormatter JsonValueFormatter = new JsonValueFormatter();

        readonly string _apiKey;
        readonly long? _eventBodyLimitBytes;
        readonly HttpClient _httpClient;
        readonly bool _useCompactFormat;

        DateTime _nextRequiredLevelCheckUtc = DateTime.UtcNow.Add(RequiredLevelCheckInterval);
        readonly ControlledLevelSwitch _controlledSwitch;

        public SeqSink(
            string serverUrl,
            string apiKey,
            int batchPostingLimit,
            TimeSpan period,
            long? eventBodyLimitBytes,
            LoggingLevelSwitch levelControlSwitch,
            HttpMessageHandler messageHandler,
            bool useCompactFormat,
            int queueSizeLimit)
            : base(batchPostingLimit, period, queueSizeLimit)
        {
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));
            _apiKey = apiKey;
            _eventBodyLimitBytes = eventBodyLimitBytes;
            _controlledSwitch = new ControlledLevelSwitch(levelControlSwitch);
            _useCompactFormat = useCompactFormat;
            _httpClient = messageHandler != null ? new HttpClient(messageHandler) : new HttpClient();
            _httpClient.BaseAddress = new Uri(SeqApi.NormalizeServerBaseAddress(serverUrl));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
                _httpClient.Dispose();
        }

        // The sink must emit at least one event on startup, and the server be
        // configured to set a specific level, before background level checks will be performed.
        protected override void OnEmptyBatch()
        {
            if (_controlledSwitch.IsActive &&
                _nextRequiredLevelCheckUtc < DateTime.UtcNow)
            {
                EmitBatch(Enumerable.Empty<LogEvent>());
            }
        }

        protected override async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            _nextRequiredLevelCheckUtc = DateTime.UtcNow.Add(RequiredLevelCheckInterval);

            string payload, payloadContentType;
            if (_useCompactFormat)
            {
                payloadContentType = SeqApi.CompactLogEventFormatMimeType;
                payload = FormatCompactPayload(events, _eventBodyLimitBytes);
            }
            else
            {
                payloadContentType = SeqApi.RawEventFormatMimeType;
                payload = FormatRawPayload(events, _eventBodyLimitBytes);
            }

            var content = new StringContent(payload, Encoding.UTF8, payloadContentType);
            if (!string.IsNullOrWhiteSpace(_apiKey))
                content.Headers.Add(SeqApi.ApiKeyHeaderName, _apiKey);
    
            var result = await _httpClient.PostAsync(SeqApi.BulkUploadResource, content).ConfigureAwait(false);
            if (!result.IsSuccessStatusCode)
                throw new LoggingFailedException($"Received failed result {result.StatusCode} when posting events to Seq");

            var returned = await result.Content.ReadAsStringAsync();
            _controlledSwitch.Update(SeqApi.ReadEventInputResult(returned));
        }

        internal static string FormatCompactPayload(IEnumerable<LogEvent> events, long? eventBodyLimitBytes)
        {
            var payload = new StringWriter();

            foreach (var logEvent in events)
            {
                var buffer = new StringWriter();

                try
                {
                    CompactJsonFormatter.FormatEvent(logEvent, buffer, JsonValueFormatter);
                }
                catch (Exception ex)
                {
                    LogNonFormattableEvent(logEvent, ex);
                    continue;
                }

                var json = buffer.ToString();
                if (CheckEventBodySize(json, eventBodyLimitBytes))
                {
                    payload.WriteLine(json);
                }
            }

            return payload.ToString();
        }

        internal static string FormatRawPayload(IEnumerable<LogEvent> events, long? eventBodyLimitBytes)
        {
            var payload = new StringWriter();
            payload.Write("{\"Events\":[");

            var delimStart = "";
            foreach (var logEvent in events)
            {
                var buffer = new StringWriter();

                try
                {
                    RawJsonFormatter.FormatContent(logEvent, buffer);
                }
                catch (Exception ex)
                {
                    LogNonFormattableEvent(logEvent, ex);
                    continue;
                }

                var json = buffer.ToString();
                if (CheckEventBodySize(json, eventBodyLimitBytes))
                {
                    payload.Write(delimStart);
                    payload.Write(json);
                    delimStart = ",";
                }
            }

            payload.Write("]}");
            return payload.ToString();
        }

        protected override bool CanInclude(LogEvent evt)
        {
            return _controlledSwitch.IsIncluded(evt);
        }

        static bool CheckEventBodySize(string json, long? eventBodyLimitBytes)
        {
            if (eventBodyLimitBytes.HasValue &&
                Encoding.UTF8.GetByteCount(json) > eventBodyLimitBytes.Value)
            {
                SelfLog.WriteLine(
                    "Event JSON representation exceeds the byte size limit of {0} set for this Seq sink and will be dropped; data: {1}",
                    eventBodyLimitBytes, json);
                return false;
            }

            return true;
        }

        static void LogNonFormattableEvent(LogEvent logEvent, Exception ex)
        {
            SelfLog.WriteLine(
                "Event at {0} with message template {1} could not be formatted into JSON for Seq and will be dropped: {2}",
                logEvent.Timestamp.ToString("o"), logEvent.MessageTemplate.Text, ex);
        }
    }
}
