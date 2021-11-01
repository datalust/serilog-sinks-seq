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
using System.IO;
using System.Text;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Json;
using Serilog.Parsing;

namespace Serilog.Sinks.Seq
{
    /// <summary>
    /// Wraps a <see cref="CompactJsonFormatter" /> to suppress formatting errors and apply the event body size
    /// limit, if any. Placeholder events are logged when an event is unable to be written itself.
    /// </summary>
    class ConstrainedBufferedFormatter : ITextFormatter
    {
        static readonly int NewLineByteCount = Encoding.UTF8.GetByteCount(Environment.NewLine);
        
        readonly long? _eventBodyLimitBytes;
        readonly CompactJsonFormatter _jsonFormatter = new CompactJsonFormatter(new JsonValueFormatter("$type"));

        public ConstrainedBufferedFormatter(long? eventBodyLimitBytes)
        {
            _eventBodyLimitBytes = eventBodyLimitBytes;
        }

        public void Format(LogEvent logEvent, TextWriter output)
        {
            Format(logEvent, output, writePlaceholders: true);
        }
        
        void Format(LogEvent logEvent, TextWriter output, bool writePlaceholders)
        {
            var buffer = new StringWriter();

            try
            {
                _jsonFormatter.Format(logEvent, buffer);
            }
            catch (Exception ex) when (writePlaceholders)
            {
                SelfLog.WriteLine(
                    "Event with message template {0} at {1} could not be formatted as JSON and will be dropped: {2}",
                    logEvent.MessageTemplate.Text, logEvent.Timestamp, ex);
                
                var placeholder = CreateNonFormattableEventPlaceholder(logEvent, ex);
                Format(placeholder, output, writePlaceholders: false);
                return;
            }

            var jsonLine = buffer.ToString();
            if (CheckEventBodySize(jsonLine, _eventBodyLimitBytes))
            {
                output.Write(jsonLine);
            }
            else
            {
                SelfLog.WriteLine(
                    "Event JSON representation exceeds the byte size limit of {0} set for this Seq sink and will be dropped; data: {1}",
                    _eventBodyLimitBytes, jsonLine);
                
                if (writePlaceholders)
                {
                    var placeholder = CreateOversizeEventPlaceholder(logEvent, jsonLine, _eventBodyLimitBytes!.Value);
                    Format(placeholder, output, writePlaceholders: false);
                }
            }
        }

        static LogEvent CreateNonFormattableEventPlaceholder(LogEvent logEvent, Exception ex)
        {
            return new LogEvent(
                logEvent.Timestamp,
                LogEventLevel.Error,
                ex,
                new MessageTemplateParser().Parse("Event with message template {OriginalMessageTemplate} could not be formatted as JSON"),
                new[]
                {
                    new LogEventProperty("OriginalMessageTemplate", new ScalarValue(logEvent.MessageTemplate.Text)),
                });
        }

        static bool CheckEventBodySize(string jsonLine, long? eventBodyLimitBytes)
        {
            if (eventBodyLimitBytes == null)
                return true;
            
            var byteCount = Encoding.UTF8.GetByteCount(jsonLine) - NewLineByteCount;
            return byteCount <= eventBodyLimitBytes;
        }
        
        static LogEvent CreateOversizeEventPlaceholder(LogEvent logEvent, string jsonLine, long eventBodyLimitBytes)
        {
            // If the limit is so constrained as to disallow sending 512 bytes + packaging, that's okay - we'll just drop
            // the placeholder, too.
            var sample = jsonLine.Substring(0, Math.Min(jsonLine.Length, 512));
            return new LogEvent(
                logEvent.Timestamp,
                LogEventLevel.Error,
                exception: null,
                new MessageTemplateParser().Parse("Event JSON representation exceeds the body size limit {EventBodyLimitBytes}; first 1024 bytes: {EventBodySample}"),
                new[]
                {
                    new LogEventProperty("EventBodyLimitBytes", new ScalarValue(eventBodyLimitBytes)),
                    new LogEventProperty("EventBodySample", new ScalarValue(sample)),
                });
        }
    }
}
