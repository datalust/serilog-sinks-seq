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
using Serilog.Events;

namespace Serilog.Sinks.Seq
{
    class SeqApi
    {
        public const string BulkUploadResource = "api/events/raw";
        public const string ApiKeyHeaderName = "X-Seq-ApiKey";
        public const string RawEventFormatMimeType = "application/json";
        public const string CompactLogEventFormatMimeType = "application/vnd.serilog.clef";

        // Why not use a JSON parser here? For a very small case, it's not
        // worth taking on the extra payload/dependency management issues that
        // a full-fledged parser will entail. If things get more sophisticated
        // we'll reevaluate.
        const string LevelMarker = "\"MinimumLevelAccepted\":\"";

        public static LogEventLevel? ReadEventInputResult(string eventInputResult)
        {
            if (eventInputResult == null) return null;

            // Seq 1.5 servers will return JSON including "MinimumLevelAccepted":x, where
            // x may be null or a JSON string representation of the equivalent LogEventLevel
            var startProp = eventInputResult.IndexOf(LevelMarker, StringComparison.Ordinal);
            if (startProp == -1)
                return null;

            var startValue = startProp + LevelMarker.Length;
            if (startValue >= eventInputResult.Length)
                return null;

            var endValue = eventInputResult.IndexOf('"', startValue);
            if (endValue == -1)
                return null;

            var value = eventInputResult.Substring(startValue, endValue - startValue);
            LogEventLevel minimumLevel;
            if (!Enum.TryParse(value, out minimumLevel))
                return null;

            return minimumLevel;
        }

        public static string NormalizeServerBaseAddress(string serverUrl)
        {
            var baseUri = serverUrl;
            if (!baseUri.EndsWith("/"))
                baseUri += "/";
            return baseUri;
        }
    }
}