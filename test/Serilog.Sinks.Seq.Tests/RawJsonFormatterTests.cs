using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Xunit;
using Serilog.Sinks.Seq.Tests.Support;

namespace Serilog.Sinks.Seq.Tests
{
    public class RawJsonFormatterTests
    {
        void AssertValidJson(Action<ILogger> act)
        {
            var output = new StringWriter();
            var formatter = new RawJsonFormatter();
            var log = new LoggerConfiguration()
                .WriteTo.Sink(new TextWriterSink(output, formatter))
                .CreateLogger();

            act(log);

            var json = output.ToString();

            // Unfortunately this will not detect all JSON formatting issues; better than nothing however.
            JObject.Parse(json);
        }

        [Fact]
        public void AnEmptyEventIsValidJson()
        {
            AssertValidJson(log => log.Information("No properties"));
        }

        [Fact]
        public void AMinimalEventIsValidJson()
        {
            AssertValidJson(log => log.Information("One {Property}", 42));
        }

        [Fact]
        public void MultiplePropertiesAreDelimited()
        {
            AssertValidJson(log => log.Information("Property {First} and {Second}", "One", "Two"));
        }

        [Fact]
        public void ExceptionsAreFormattedToValidJson()
        {
            AssertValidJson(log => log.Information(new DivideByZeroException(), "With exception"));
        }

        [Fact]
        public void ExceptionAndPropertiesAreValidJson()
        {
            AssertValidJson(log => log.Information(new DivideByZeroException(), "With exception and {Property}", 42));
        }

        [Fact]
        public void RenderingsAreValidJson()
        {
            AssertValidJson(log => log.Information("One {Rendering:x8}", 42));
        }

        [Fact]
        public void MultipleRenderingsAreDelimited()
        {
            AssertValidJson(log => log.Information("Rendering {First:x8} and {Second:x8}", 1, 2));
        }
    }
}
