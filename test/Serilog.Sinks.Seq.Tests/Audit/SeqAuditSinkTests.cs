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
            using (var logger = new LoggerConfiguration()
                .AuditTo.Seq("https://example.tld")
                .CreateLogger())
            {
                var ex = Assert.Throws<AggregateException>(() => logger.Information("This is an audit event"));
                var baseException = ex.GetBaseException();
                Assert.IsType<HttpRequestException>(baseException);
            }
        }

        [Fact]
        public void RemoteCommunicationErrorsPropagateToCallerWhenAuditing()
        {
            using (var logger = new LoggerConfiguration()
                .AuditTo.Seq("https://serilog.net/test/404")
                .CreateLogger())
            {
                var ex = Assert.Throws<AggregateException>(() => logger.Information("This is an audit event"));
                var baseException = ex.GetBaseException();
                Assert.IsType<LoggingFailedException>(baseException);
            }
        }
    }
}
