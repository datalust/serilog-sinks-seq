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
            var jsonString = json.ToString();
            Assert.Contains("could not be formatted", jsonString);
            Assert.Contains("OriginalMessageTemplate\":\"Hello, ", jsonString);
        }
        
        [Fact]
        public void PlaceholdersAreLoggedWhenTheEventSizeLimitIsExceeded()
        {
            var evt = Some.LogEvent("Hello, {Name}!", new string('a', 10000));
            var formatter = new ConstrainedBufferedFormatter(2000);
            var json = new StringWriter();
            formatter.Format(evt, json);
            var jsonString = json.ToString();
            Assert.Contains("exceeds the body size limit", jsonString);
            Assert.Contains("\"EventBodySample\"", jsonString);
            Assert.Contains("aaaaa", jsonString);
        }
    }
}
