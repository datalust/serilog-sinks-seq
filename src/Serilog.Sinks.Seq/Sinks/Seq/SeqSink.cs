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
using Serilog.Formatting.Json;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.Seq
{
    class SeqSink : PeriodicBatchingSink
    {
        readonly string _apiKey;
        readonly long? _eventBodyLimitBytes;
        readonly HttpClient _httpClient;
        const string BulkUploadResource = "api/events/raw";
        const string ApiKeyHeaderName = "X-Seq-ApiKey";

        // If non-null, then background level checks will be performed; set either through the constructor
        // or in response to a level specification from the server. Never set to null after being made non-null.
        LoggingLevelSwitch _levelControlSwitch;
        static readonly TimeSpan RequiredLevelCheckInterval = TimeSpan.FromMinutes(2);
        DateTime _nextRequiredLevelCheckUtc = DateTime.UtcNow.Add(RequiredLevelCheckInterval);

        public const int DefaultBatchPostingLimit = 1000;
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(2);

        public SeqSink(string serverUrl, HttpMessageHandler messageHandler, string apiKey, int batchPostingLimit, TimeSpan period, long? eventBodyLimitBytes, LoggingLevelSwitch levelControlSwitch)
            : base(batchPostingLimit, period)
        {
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));
            _apiKey = apiKey;
            _eventBodyLimitBytes = eventBodyLimitBytes;
            _levelControlSwitch = levelControlSwitch;

            var baseUri = serverUrl;
            if (!baseUri.EndsWith("/"))
                baseUri += "/";

            _httpClient = messageHandler != null ? new HttpClient(messageHandler) : new HttpClient();
            _httpClient.BaseAddress = new Uri(baseUri);
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
            if (_levelControlSwitch != null &&
                _nextRequiredLevelCheckUtc < DateTime.UtcNow)
            {
                EmitBatch(Enumerable.Empty<LogEvent>());
            }
        }

        protected override async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            _nextRequiredLevelCheckUtc = DateTime.UtcNow.Add(RequiredLevelCheckInterval);

            var payload = new StringWriter();
            payload.Write("{\"Events\":[");

            var formatter = new JsonFormatter(closingDelimiter: "");
            var delimStart = "";
            foreach (var logEvent in events)
            {
                if (_eventBodyLimitBytes.HasValue)
                {
                    var scratch = new StringWriter();
                    formatter.Format(logEvent, scratch);
                    var buffered = scratch.ToString();

                    if (Encoding.UTF8.GetByteCount(buffered) > _eventBodyLimitBytes.Value)
                    {
                        SelfLog.WriteLine("Event JSON representation exceeds the byte size limit of {0} set for this sink and will be dropped; data: {1}", _eventBodyLimitBytes, buffered);
                    }
                    else
                    {
                        payload.Write(delimStart);
                        payload.Write(buffered);
                        delimStart = ",";
                    }
                }
                else
                {
                    payload.Write(delimStart);
                    formatter.Format(logEvent, payload);
                    delimStart = ",";
                }
            }

            payload.Write("]}");

            var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
            if (!string.IsNullOrWhiteSpace(_apiKey))
                content.Headers.Add(ApiKeyHeaderName, _apiKey);
    
            var result = await _httpClient.PostAsync(BulkUploadResource, content);
            if (!result.IsSuccessStatusCode)
                throw new LoggingFailedException($"Received failed result {result.StatusCode} when posting events to Seq");

            var returned = await result.Content.ReadAsStringAsync();
            var minimumAcceptedLevel = SeqApi.ReadEventInputResult(returned);
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

        protected override bool CanInclude(LogEvent evt)
        {
            var levelControlSwitch = _levelControlSwitch;
            return levelControlSwitch == null ||
                (int)levelControlSwitch.MinimumLevel <= (int)evt.Level;
        }
    }
}
