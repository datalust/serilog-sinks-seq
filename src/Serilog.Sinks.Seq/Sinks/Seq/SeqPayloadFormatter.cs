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
using System.Text;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Json;

namespace Serilog.Sinks.Seq
{
    static class SeqPayloadFormatter
    {
        static readonly JsonValueFormatter JsonValueFormatter = new JsonValueFormatter();
        
        public static string FormatCompactPayload(IEnumerable<LogEvent> events, long? eventBodyLimitBytes)
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

        public static string FormatRawPayload(IEnumerable<LogEvent> events, long? eventBodyLimitBytes)
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

        static void LogNonFormattableEvent(LogEvent logEvent, Exception ex)
        {
            SelfLog.WriteLine(
                "Event at {0} with message template {1} could not be formatted into JSON for Seq and will be dropped: {2}",
                logEvent.Timestamp.ToString("o"), logEvent.MessageTemplate.Text, ex);
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

    }
}