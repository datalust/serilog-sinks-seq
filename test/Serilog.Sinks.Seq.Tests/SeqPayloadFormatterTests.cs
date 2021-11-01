using System.IO;
using Serilog.Sinks.Seq.Tests.Support;
using Xunit;

namespace Serilog.Sinks.Seq.Tests
{
    public class ConstrainedBufferedFormatterTests
    {
        [Fact]
        public void EventsAreFormattedIntoCompactJsonPayloads()
        {
            var evt = Some.LogEvent("Hello, {Name}!", "Alice");
            var formatter = new ConstrainedBufferedFormatter(null);
            var json = new StringWriter();
            formatter.Format(evt, json);
            Assert.Contains("Name\":\"Alice", json.ToString());
        }

        [Fact]
        public void PlaceholdersAreLoggedWhenCompactJsonRenderingFails()
        {
            var evt = Some.LogEvent(new NastyException(), "Hello, {Name}!", "Alice");
            var formatter = new ConstrainedBufferedFormatter(null);
            var json = new StringWriter();
            formatter.Format(evt, json);
            Assert.Contains("OriginalMessageTemplate\":\"Hello, ", json.ToString());
        }
    }
}
