using System.Net;
using System.Threading.Tasks;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.Seq.Batched;
using Serilog.Sinks.Seq.Http;
using Serilog.Sinks.Seq.Tests.Support;
using Xunit;

namespace Serilog.Sinks.Seq.Tests.Batched;

public class BatchedSeqSinkTests
{
    [Fact]
    public void BatchedSinkDisposesIngestionApi()
    {
        var api = new TestIngestionApi();
        var sink = new BatchedSeqSink(api, new SeqCompactJsonFormatter(), null, new ControlledLevelSwitch());
        Assert.False(api.IsDisposed);
            
        sink.Dispose();
            
        Assert.True(api.IsDisposed);
    }
        
    [Fact]
    public async Task EventsAreFormattedIntoPayloads()
    {
        var api = new TestIngestionApi();
        var sink = new BatchedSeqSink(api, new SeqCompactJsonFormatter(), null, new ControlledLevelSwitch());

        await sink.EmitBatchAsync(new[]
        {
            Some.InformationEvent("first"),
            Some.InformationEvent("second")
        });

        var emitted = await api.GetPayloadAsync();

        Assert.Contains("first", emitted.Payload);
        Assert.Contains("second", emitted.Payload);
    }

    [Fact]
    public async Task MinimumLevelIsControlled()
    {
        const LogEventLevel originalLevel = LogEventLevel.Debug, newLevel = LogEventLevel.Error;
        var levelSwitch = new LoggingLevelSwitch(originalLevel);
        var api = new TestIngestionApi(_ => Task.FromResult(new IngestionResult(true, HttpStatusCode.Accepted, newLevel)));
        var sink = new BatchedSeqSink(api, new SeqCompactJsonFormatter(), null, new ControlledLevelSwitch(levelSwitch));

        await sink.EmitBatchAsync(new[] { Some.InformationEvent() });
            
        Assert.Equal(newLevel, levelSwitch.MinimumLevel);
    }
}