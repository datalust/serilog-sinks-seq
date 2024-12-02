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

using Serilog.Debugging;
using Serilog.Events;

namespace Serilog.Sinks.Seq.Http;

/// <summary>
/// A substitutable interface type for the Seq HTTP ingestion API.
/// </summary>
/// <remarks>A class rather than an interface for convenience reasons (and because disposable interfaces are awful).</remarks>
abstract class SeqIngestionApi : IDisposable
{
    /// <summary>
    /// The media type describing the original JSON-based payload format. Use is now discouraged. Remains here only
    /// so that durable buffer files written by earlier versions of the sink can still be read and ingested.
    /// </summary>
    public const string RawEventFormatMediaType = "application/json";
        
    /// <summary>
    /// Media type for the modern CLEF payload format.
    /// </summary>
    public const string CompactLogEventFormatMediaType = "application/vnd.serilog.clef";
        
    /// <summary>
    /// A valid but empty payload in the <see cref="CompactLogEventFormatMediaType"/> format.
    /// </summary>
    public const string EmptyClefPayload = "";

    /// <summary>
    /// Ingest <paramref name="clefPayload" />.
    /// </summary>
    /// <param name="clefPayload">Log events in CLEF format.</param>
    /// <returns>The minimum level accepted by the Seq server (if any is specified).</returns>
    /// <exception cref="LoggingFailedException">The events could not be ingested.</exception>
    /// <exception cref="HttpRequestException">The ingestion request could not be sent.</exception>
    public async Task<LogEventLevel?> IngestAsync(string clefPayload)
    {
        var result = await TryIngestAsync(clefPayload, CompactLogEventFormatMediaType).ConfigureAwait(false);

        if (!result.Succeeded)
            throw new LoggingFailedException($"Received failed result {result.StatusCode} when posting events to Seq.");

        return result.MinimumAcceptedLevel;
    }

    /// <summary>
    /// Attempt to ingest <paramref name="payload"/>.
    /// </summary>
    /// <param name="payload">The text-formatted payload to ingest.</param>
    /// <param name="mediaType">The media type describing the payload.</param>
    /// <returns>An <see cref="IngestionResult"/> with the response from the ingestion API.</returns>
    /// <exception cref="HttpRequestException">The ingestion request could not be sent.</exception>
    public abstract Task<IngestionResult> TryIngestAsync(string payload, string mediaType);
        
    /// <inheritdoc/>
    public virtual void Dispose() { }
}