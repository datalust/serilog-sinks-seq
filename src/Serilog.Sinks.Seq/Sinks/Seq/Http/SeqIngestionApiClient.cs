// Copyright © Serilog Contributors
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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Serilog.Events;

namespace Serilog.Sinks.Seq.Http
{
    /// <summary>
    /// Implements <see cref="SeqIngestionApi"/> over <see cref="HttpClient" />; this is the runtime implementation of
    /// the ingestion API.
    /// </summary>
    sealed class SeqIngestionApiClient : SeqIngestionApi
    {
        const string BulkUploadResource = "api/events/raw";
        const string ApiKeyHeaderName = "X-Seq-ApiKey";
        
        readonly string? _apiKey;
        readonly HttpClient _httpClient;

        public SeqIngestionApiClient(string serverUrl, string? apiKey, HttpMessageHandler? messageHandler)
        {
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));
            _apiKey = apiKey;
            _httpClient = messageHandler != null
                    ? new HttpClient(messageHandler)
                    :
#if SOCKETS_HTTP_HANDLER_ALWAYS_DEFAULT
                    new HttpClient(new SocketsHttpHandler
                    {
                        // The default value is infinite; this causes problems for long-running processes if DNS changes
                        // require that the Seq API be accessed at a different IP address. Setting a timeout here puts
                        // an upper bound on the duration of DNS-related outages, while hopefully incurring only infrequent
                        // connection reestablishment costs.
                        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
                    })
#else
                    new HttpClient()
#endif
                ;
            
            _httpClient.BaseAddress = new Uri(NormalizeServerBaseAddress(serverUrl));
        }

        public override async Task<IngestionResult> TryIngestAsync(string payload, string mediaType)
        {
            var content = new StringContent(payload, Encoding.UTF8, mediaType);
            if (!string.IsNullOrWhiteSpace(_apiKey))
                content.Headers.Add(ApiKeyHeaderName, _apiKey);
    
            using var response = await _httpClient.PostAsync(BulkUploadResource, content).ConfigureAwait(false);
            
            if (!response.IsSuccessStatusCode)
                return new(false, response.StatusCode, null);
            
            var returned = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var minimumLevel = ReadIngestionResult(returned);
            return new(true, response.StatusCode, minimumLevel);
        }
        
        public override void Dispose()
        {
            _httpClient.Dispose();
        }
        
        // Why not use a JSON parser here? For a very small case, it's not
        // worth taking on the extra payload/dependency management issues that
        // a full-fledged parser will entail. If things get more sophisticated
        // we'll reevaluate.
        const string LevelMarker = "\"MinimumLevelAccepted\":\"";

        internal static LogEventLevel? ReadIngestionResult(string? ingestionResult)
        {
            if (ingestionResult == null) return null;

            // Seq 1.5 servers will return JSON including "MinimumLevelAccepted":x, where
            // x may be null or a JSON string representation of the equivalent LogEventLevel
            var startProp = ingestionResult.IndexOf(LevelMarker, StringComparison.Ordinal);
            if (startProp == -1)
                return null;

            var startValue = startProp + LevelMarker.Length;
            if (startValue >= ingestionResult.Length)
                return null;

            var endValue = ingestionResult.IndexOf('"', startValue);
            if (endValue == -1)
                return null;

            var value = ingestionResult.Substring(startValue, endValue - startValue);
            if (!Enum.TryParse(value, out LogEventLevel minimumLevel))
                return null;

            return minimumLevel;
        }

        internal static string NormalizeServerBaseAddress(string serverUrl)
        {
            var baseUri = serverUrl;
            if (!baseUri.EndsWith("/"))
                baseUri += "/";
            return baseUri;
        }
    }
}
