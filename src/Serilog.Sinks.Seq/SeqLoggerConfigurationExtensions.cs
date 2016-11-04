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
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.Seq;
using System.Net.Http;

namespace Serilog
{
    /// <summary>
    /// Extends Serilog configuration to write events to Seq.
    /// </summary>
    public static class SeqLoggerConfigurationExtensions
    {
        /// <summary>
        /// Adds a sink that writes log events to a <a href="https://getseq.net">Seq</a> server.
        /// </summary>
        /// <param name="loggerSinkConfiguration">The logger configuration.</param>
        /// <param name="serverUrl">The base URL of the Seq server that log events will be written to.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required 
        /// in order to write an event to the sink.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="bufferBaseFilename">Path for a set of files that will be used to buffer events until they
        /// can be successfully transmitted across the network. Individual files will be created using the
        /// pattern <paramref name="bufferBaseFilename"/>-{Date}.json.</param>
        /// <param name="apiKey">A Seq <i>API key</i> that authenticates the client to the Seq server.</param>
        /// <param name="bufferFileSizeLimitBytes">The maximum size, in bytes, to which the buffer
        /// log file for a specific date will be allowed to grow. By default no limit will be applied.</param>
        /// <param name="eventBodyLimitBytes">The maximum size, in bytes, that the JSON representation of
        /// an event may take before it is dropped rather than being sent to the Seq server. Specify null for no limit.
        /// The default is 265 KB.</param>
        /// <param name="controlLevelSwitch">If provided, the switch will be updated based on the Seq server's level setting
        /// for the corresponding API key. Passing the same key to MinimumLevel.ControlledBy() will make the whole pipeline
        /// dynamically controlled. Do not specify <paramref name="restrictedToMinimumLevel"/> with this setting.</param>
        /// <param name="messageHandler">Used to construct the HttpClient that will send the log meesages to Seq.</param>
        /// <param name="retainedInvalidPayloadsLimitBytes">A soft limit for the number of bytes to use for storing failed requests.  
        /// The limit is soft in that it can be exceeded by any single error payload, but in that case only that single error
        /// payload will be retained.</param>
        /// <param name="compact">Use the compact log event format defined by
        /// <a href="https://github.com/serilog/serilog-formatting-compact">Serilog.Formatting.Compact</a>. Has no effect on
        /// durable log shipping. Requires Seq 3.3+.</param>
        /// <param name="queueSizeLimit">The maximum number of events that will be held in-memory while waiting to ship them to
        /// Seq. Beyond this limit, events will be dropped. The default is 100,000. Has no effect on
        /// durable log shipping.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration Seq(
            this LoggerSinkConfiguration loggerSinkConfiguration,
            string serverUrl,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            int batchPostingLimit = SeqSink.DefaultBatchPostingLimit,
            TimeSpan? period = null,
            string apiKey = null,
            string bufferBaseFilename = null,
            long? bufferFileSizeLimitBytes = null,
            long? eventBodyLimitBytes = 256*1024,
            LoggingLevelSwitch controlLevelSwitch = null,
            HttpMessageHandler messageHandler = null,
            long? retainedInvalidPayloadsLimitBytes = null,
            bool compact = false,
            int queueSizeLimit = SeqSink.DefaultQueueSizeLimit)
        {
            if (loggerSinkConfiguration == null) throw new ArgumentNullException(nameof(loggerSinkConfiguration));
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));
            if (bufferFileSizeLimitBytes.HasValue && bufferFileSizeLimitBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferFileSizeLimitBytes), "Negative value provided; file size limit must be non-negative.");
            if (queueSizeLimit < 0)
                throw new ArgumentOutOfRangeException(nameof(queueSizeLimit), "Queue size limit must be non-zero.");

            var defaultedPeriod = period ?? SeqSink.DefaultPeriod;

            ILogEventSink sink;

            if (bufferBaseFilename == null)
            {
                sink = new SeqSink(
                    serverUrl,
                    apiKey,
                    batchPostingLimit,
                    defaultedPeriod,
                    eventBodyLimitBytes,
                    controlLevelSwitch,
                    messageHandler,
                    compact,
                    queueSizeLimit);
            }
            else
            {
#if DURABLE
                sink = new DurableSeqSink(
                    serverUrl,
                    bufferBaseFilename,
                    apiKey,
                    batchPostingLimit,
                    defaultedPeriod,
                    bufferFileSizeLimitBytes,
                    eventBodyLimitBytes,
                    controlLevelSwitch,
                    messageHandler,
                    retainedInvalidPayloadsLimitBytes);
#else
                // We keep the API consistent for easier packaging and to support bait-and-switch.
                throw new NotSupportedException("Durable log shipping is not supported on this platform.");
#endif
            }

            return loggerSinkConfiguration.Sink(sink, restrictedToMinimumLevel);
        }

        /// <summary>
        /// Adds a sink that writes audit log events to a <a href="https://getseq.net">Seq</a> server. Auditing writes are
        /// synchronous and non-batched; any failures will propagate to the caller immediately as exceptions.
        /// </summary>
        /// <param name="loggerAuditSinkConfiguration">The logger configuration.</param>
        /// <param name="serverUrl">The base URL of the Seq server that log events will be written to.</param>
        /// <param name="restrictedToMinimumLevel">The minimum log event level required 
        /// in order to write an event to the sink.</param>
        /// <param name="apiKey">A Seq <i>API key</i> that authenticates the client to the Seq server.</param>
        /// <param name="messageHandler">Used to construct the HttpClient that will send the log meesages to Seq.</param>
        /// <param name="compact">Use the compact log event format defined by
        /// <a href="https://github.com/serilog/serilog-formatting-compact">Serilog.Formatting.Compact</a>. Has no effect on
        /// durable log shipping. Requires Seq 3.3+.</param>
        /// <returns>Logger configuration, allowing configuration to continue.</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration Seq(
            this LoggerAuditSinkConfiguration loggerAuditSinkConfiguration,
            string serverUrl,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string apiKey = null,
            HttpMessageHandler messageHandler = null,
            bool compact = false)
        {
            if (loggerAuditSinkConfiguration == null) throw new ArgumentNullException(nameof(loggerAuditSinkConfiguration));
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));

            return loggerAuditSinkConfiguration.Sink(
                new SeqAuditSink(serverUrl, apiKey, messageHandler, compact),
                restrictedToMinimumLevel);
        }
    }
}
