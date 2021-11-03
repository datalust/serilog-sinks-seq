// Serilog.Sinks.Seq Copyright 2014-2019 Serilog Contributors
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
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

namespace Serilog.Sinks.Seq
{
    class SeqSink : IBatchedLogEventSink, IDisposable
    {
        public const int DefaultBatchPostingLimit = 1000;
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(2);
        public const int DefaultQueueSizeLimit = 100000;

        static readonly TimeSpan RequiredLevelCheckInterval = TimeSpan.FromMinutes(2);

        readonly string? _apiKey;
        readonly ConstrainedBufferedFormatter _formatter;
        readonly HttpClient _httpClient;

        DateTime _nextRequiredLevelCheckUtc = DateTime.UtcNow.Add(RequiredLevelCheckInterval);
        readonly ControlledLevelSwitch _controlledSwitch;

        public SeqSink(
            string serverUrl,
            string? apiKey,
            long? eventBodyLimitBytes,
            ControlledLevelSwitch controlledSwitch,
            HttpMessageHandler? messageHandler)
        {
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));
            _controlledSwitch = controlledSwitch ?? throw new ArgumentNullException(nameof(controlledSwitch));
            _apiKey = apiKey;
            _httpClient = messageHandler != null ? new HttpClient(messageHandler) : new HttpClient();
            _httpClient.BaseAddress = new Uri(SeqApi.NormalizeServerBaseAddress(serverUrl));
            _formatter = new ConstrainedBufferedFormatter(eventBodyLimitBytes);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        // The sink must emit at least one event on startup, and the server be
        // configured to set a specific level, before background level checks will be performed.
        public async Task OnEmptyBatchAsync()
        {
            if (_controlledSwitch.IsActive &&
                _nextRequiredLevelCheckUtc < DateTime.UtcNow)
            {
                await EmitBatchAsync(Enumerable.Empty<LogEvent>());
            }
        }

        public async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            _nextRequiredLevelCheckUtc = DateTime.UtcNow.Add(RequiredLevelCheckInterval);

            var payload = new StringWriter();
            foreach (var evt in events)
            {
                _formatter.Format(evt, payload);
            }

            var content = new StringContent(payload.ToString(), Encoding.UTF8, SeqApi.CompactLogEventFormatMimeType);
            if (!string.IsNullOrWhiteSpace(_apiKey))
                content.Headers.Add(SeqApi.ApiKeyHeaderName, _apiKey);
    
            var result = await _httpClient.PostAsync(SeqApi.BulkUploadResource, content).ConfigureAwait(false);
            if (!result.IsSuccessStatusCode)
                throw new LoggingFailedException($"Received failed result {result.StatusCode} when posting events to Seq");

            var returned = await result.Content.ReadAsStringAsync();
            _controlledSwitch.Update(SeqApi.ReadEventInputResult(returned));
        }
    }
}
