using Serilog.Sinks.Seq.Tests.Support;
using Xunit;

namespace Serilog.Sinks.Seq.Tests
{
    public class SeqSinkTests
    {
        [Fact]
        public void EventsAreFormattedIntoJsonPayloads()
        {
            var evt = Some.LogEvent("Hello, {Name}!", "Alice");
            var json = SeqSink.FormatRawPayload(new[] {evt}, null);
            Assert.Contains("Name\":\"Alice", json);
        }

        [Fact]
        public void EventsAreDroppedWhenJsonRenderingFails()
        {
            var evt = Some.LogEvent(new NastyException(), "Hello, {Name}!", "Alice");
            var json = SeqSink.FormatRawPayload(new[] { evt }, null);
            Assert.Contains("[]", json);
        }

        [Fact]
        public void EventsAreFormattedIntoCompactJsonPayloads()
        {
            var evt = Some.LogEvent("Hello, {Name}!", "Alice");
            var json = SeqSink.FormatCompactPayload(new[] { evt }, null);
            Assert.Contains("Name\":\"Alice", json);
        }

        [Fact]
        public void EventsAreDroppedWhenCompactJsonRenderingFails()
        {
            var evt = Some.LogEvent(new NastyException(), "Hello, {Name}!", "Alice");
            var json = SeqSink.FormatCompactPayload(new[] { evt }, null);
            Assert.Empty(json);
        }
    }
}
