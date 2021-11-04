using System;
using System.Net.Http;
using Serilog.Debugging;
using Xunit;

namespace Serilog.Sinks.Seq.Tests.Audit
{
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
    }
}
