using Serilog.Sinks.Seq.Tests.Support;
using Xunit;

namespace Serilog.Sinks.Seq.Tests
{
    public class SeqPayloadFormatterTests
    {
        [Fact]
        public void EventsAreFormattedIntoCompactJsonPayloads()
        {
            var evt = Some.LogEvent("Hello, {Name}!", "Alice");
            var json = SeqPayloadFormatter.FormatCompactPayload(new[] { evt }, null);
            Assert.Contains("Name\":\"Alice", json);
        }

        [Fact]
        public void EventsAreDroppedWhenCompactJsonRenderingFails()
        {
            var evt = Some.LogEvent(new NastyException(), "Hello, {Name}!", "Alice");
            var json = SeqPayloadFormatter.FormatCompactPayload(new[] { evt }, null);
            Assert.Empty(json);
        }
    }
}
