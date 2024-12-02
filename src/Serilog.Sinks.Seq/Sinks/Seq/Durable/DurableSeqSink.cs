// Serilog.Sinks.Seq Copyright © Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Serilog.Core;
using Serilog.Events;
using System.Text;
using Serilog.Formatting;
using Serilog.Sinks.Seq.Http;

namespace Serilog.Sinks.Seq.Durable;

sealed class DurableSeqSink : ILogEventSink, IDisposable
#if ASYNC_DISPOSE
        , IAsyncDisposable
#endif
{
    readonly HttpLogShipper _shipper;
    readonly Logger _sink;

    public DurableSeqSink(
        SeqIngestionApi ingestionApi,
        ITextFormatter payloadFormatter,
        string bufferBaseFilename,
        int batchPostingLimit,
        TimeSpan period,
        long? bufferSizeLimitBytes,
        long? eventBodyLimitBytes,
        ControlledLevelSwitch controlledSwitch,
        long? retainedInvalidPayloadsLimitBytes)
    {
        if (bufferBaseFilename == null) throw new ArgumentNullException(nameof(bufferBaseFilename));

        var fileSet = new FileSet(bufferBaseFilename);

        _shipper = new HttpLogShipper(
            fileSet,
            ingestionApi,
            batchPostingLimit, 
            period, 
            eventBodyLimitBytes,
            controlledSwitch,
            retainedInvalidPayloadsLimitBytes,
            bufferSizeLimitBytes);

        const long individualFileSizeLimitBytes = 100L * 1024 * 1024;
        _sink = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(new ConstrainedBufferedFormatter(eventBodyLimitBytes, payloadFormatter),
                fileSet.RollingFilePathFormat,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: individualFileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: null,
                encoding: Encoding.UTF8)
            .CreateLogger();
    }

    public void Dispose()
    {
        _sink.Dispose();
        _shipper.Dispose();
    }
        
#if ASYNC_DISPOSE
        public async System.Threading.Tasks.ValueTask DisposeAsync()
        {
            await _sink.DisposeAsync().ConfigureAwait(false);
            await _shipper.DisposeAsync().ConfigureAwait(false);
        }
#endif

    public void Emit(LogEvent logEvent)
    {
        // This is a lagging indicator, but the network bandwidth usage benefits
        // are worth the ambiguity.
        if (_shipper.IsIncluded(logEvent))
        {
            _sink.Write(logEvent);
        }
    }
}