using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Sinks.Seq.Durable;
using Serilog.Sinks.Seq.Http;
using Serilog.Sinks.Seq.Tests.Support;
using Xunit;

namespace Serilog.Sinks.Seq.Tests.Durable;

public class DurableSeqSinkTests
{
    [Fact]
    public void SinkCanBeDisposedCleanlyWhenUnused()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"), "buffer");
        var sink = new DurableSeqSink(
            new TestIngestionApi(_ => Task.FromResult(new IngestionResult(true, HttpStatusCode.Accepted, null))),
            new SeqCompactJsonFormatter(),
            path,
            100,
            TimeSpan.FromSeconds(1),
            null,
            null,
            new ControlledLevelSwitch(),
            null);
        
        // No events written, so files/paths should not exist;
        Assert.False(Directory.Exists(path));

        using var collector = new SelfLogCollector();
        sink.Dispose();
        Assert.Empty(collector.Messages);
    }
}