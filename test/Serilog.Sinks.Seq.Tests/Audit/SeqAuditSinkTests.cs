using System;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Seq.Audit;
using Serilog.Sinks.Seq.Tests.Support;
using Xunit;

namespace Serilog.Sinks.Seq.Tests.Audit;

public class SeqAuditSinkTests
{
    [Fact]
    public void EarlyCommunicationErrorsPropagateToCallerWhenAuditing()
    {
        using var logger = new LoggerConfiguration()
            .AuditTo.Seq("https://example.tld")
            .CreateLogger();
        var ex = Assert.Throws<AggregateException>(() => logger.Information("This is an audit event"));
        var baseException = ex.GetBaseException();
        Assert.IsType<HttpRequestException>(baseException);
    }

    [Fact] // This test requires an outbound connection in order to execute properly.
    public void RemoteCommunicationErrorsPropagateToCallerWhenAuditing()
    {
        using var logger = new LoggerConfiguration()
            .AuditTo.Seq("https://datalust.co/error/404")
            .CreateLogger();
            
        var ex = Assert.Throws<AggregateException>(() => logger.Information("This is an audit event"));
            
        var baseException = ex.GetBaseException();
        Assert.IsType<LoggingFailedException>(baseException);
    }

    [Fact]
    public void AuditSinkDisposesIngestionApi()
    {
        var api = new TestIngestionApi();
        var sink = new SeqAuditSink(api, new CompactJsonFormatter());
        Assert.False(api.IsDisposed);
            
        sink.Dispose();
            
        Assert.True(api.IsDisposed);
    }

    [Fact]
    public async Task AuditSinkEmitsIndividualEvents()
    {
        LogEvent evt1 = Some.InformationEvent("first"), evt2 = Some.InformationEvent("second");
            
        var api = new TestIngestionApi();
        var sink = new SeqAuditSink(api, new CompactJsonFormatter());
            
        sink.Emit(evt1);
        sink.Emit(evt2);

        var first = await api.GetPayloadAsync();
        Assert.Contains("first", first.Payload);
            
        var second = await api.GetPayloadAsync();
        Assert.Contains("second", second.Payload);
    }

    [Fact]
    public void AuditSinkPropagatesExceptions()
    {
        var expected = new Exception("Test");
        var api = new TestIngestionApi(_ => throw expected);
        var sink = new SeqAuditSink(api, new CompactJsonFormatter());
            
        var thrown = Assert.Throws<AggregateException>(() => sink.Emit(Some.InformationEvent()));
            
        Assert.Equal(expected, thrown.GetBaseException());
    }
}