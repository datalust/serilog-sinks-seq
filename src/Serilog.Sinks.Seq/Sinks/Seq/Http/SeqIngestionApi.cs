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
using System.Threading.Tasks;
using Serilog.Debugging;
using Serilog.Events;

namespace Serilog.Sinks.Seq.Http
{
    abstract class SeqIngestionApi : IDisposable
    {
        public const string RawEventFormatMediaType = "application/json";
        public const string CompactLogEventFormatMediaType = "application/vnd.serilog.clef";
        public const string NoPayload = "";

        /// <summary>
        /// Ingest <paramref name="clefPayload" />.
        /// </summary>
        /// <param name="clefPayload">Log events in CLEF format.</param>
        /// <returns>The minimum level accepted by the Seq server (if any is specified).</returns>
        /// <exception cref="LoggingFailedException">The events could not be ingested.</exception>
        public async Task<LogEventLevel?> IngestAsync(string clefPayload)
        {
            var result = await TryIngestAsync(clefPayload, CompactLogEventFormatMediaType);

            if (!result.Succeeded)
                throw new LoggingFailedException($"Received failed result {result.StatusCode} when posting events to Seq.");

            return result.MinimumAcceptedLevel;
        }

        public abstract Task<IngestionResult> TryIngestAsync(string payload, string mediaType);
        
        public virtual void Dispose() { }
    }
}
