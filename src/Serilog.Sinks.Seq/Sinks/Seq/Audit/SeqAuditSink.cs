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
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Json;

namespace Serilog.Sinks.Seq.Audit
{
    sealed class SeqAuditSink : ILogEventSink, IDisposable
    {
        readonly string? _apiKey;
        readonly HttpClient _httpClient;

        static readonly JsonValueFormatter JsonValueFormatter = new("$type");

        public SeqAuditSink(
            string serverUrl,
            string? apiKey,
            HttpMessageHandler? messageHandler)
        {
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));
            _httpClient = messageHandler != null ? new HttpClient(messageHandler) : new HttpClient();
            _httpClient.BaseAddress = new Uri(SeqApi.NormalizeServerBaseAddress(serverUrl));
            _apiKey = apiKey;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public void Emit(LogEvent logEvent)
        {
            EmitAsync(logEvent).Wait();
        }

        async Task EmitAsync(LogEvent logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

            var payload = new StringWriter();
            CompactJsonFormatter.FormatEvent(logEvent, payload, JsonValueFormatter);

            var content = new StringContent(payload.ToString(), Encoding.UTF8, SeqApi.CompactLogEventFormatMimeType);
            if (!string.IsNullOrWhiteSpace(_apiKey))
                content.Headers.Add(SeqApi.ApiKeyHeaderName, _apiKey);
    
            var result = await _httpClient.PostAsync(SeqApi.BulkUploadResource, content).ConfigureAwait(false);
            if (!result.IsSuccessStatusCode)
                throw new LoggingFailedException($"Received failed result {result.StatusCode} when posting events to Seq.");
        }
    }
}
