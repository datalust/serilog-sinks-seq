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

using System.Net;
using Serilog.Events;

namespace Serilog.Sinks.Seq.Http;

/// <summary>
/// The result of a POST to the ingestion API.
/// </summary>
readonly struct IngestionResult
{
    /// <summary>
    /// True if the payload was ingested successfully; otherwise, false.
    /// </summary>
    public bool Succeeded { get; }
        
    /// <summary>
    /// The status code returned from the ingestion endpoint.
    /// </summary>
    public HttpStatusCode StatusCode { get; }
        
    /// <summary>
    /// The minimum level accepted by the API. This will be null when the ingestion attempt failed, and
    /// when the server is accepting all events.
    /// </summary>
    public LogEventLevel? MinimumAcceptedLevel { get; }

    /// <summary>
    /// Construct an <see cref="IngestionResult"/>.
    /// </summary>
    /// <param name="succeeded">True if the payload was ingested successfully; otherwise, false.</param>
    /// <param name="statusCode">The status code returned from the ingestion endpoint.</param>
    /// <param name="minimumAcceptedLevel">The minimum level accepted by the API. This will be null when the ingestion attempt failed, and
    /// when the server is accepting all events.</param>
    public IngestionResult(bool succeeded, HttpStatusCode statusCode, LogEventLevel? minimumAcceptedLevel)
    {
        Succeeded = succeeded;
        StatusCode = statusCode;
        MinimumAcceptedLevel = minimumAcceptedLevel;
    }
}