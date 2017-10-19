using System.Text;
using Newtonsoft.Json;
using Serilog.Sinks.Seq.Durable;
using Serilog.Sinks.Seq.Tests.Support;
using Xunit;
using IOFile = System.IO.File;

namespace Serilog.Sinks.Seq.Tests.Durable
{
    public class PayloadReaderTests
    {
        [Fact]
        public void ReadsEventsFromBufferFiles()
        {
            using (var tmp = new TempFolder())
            {
                var fn = tmp.AllocateFilename("json");
                IOFile.WriteAllText(fn, Resources.ThreeBufferedEvents_json, Encoding.UTF8);
                var position = new FileSetPosition(0, fn);
                var count = 0;
                var payload = PayloadReader.ReadPayload(1000, null, ref position, ref count);

                Assert.Equal(3, count);
                Assert.Equal(579, position.NextLineStart);
                Assert.Equal(fn, position.File);

                var data = JsonConvert.DeserializeObject<dynamic>(payload);
                var events = data["Events"];
                Assert.NotNull(events);
                Assert.Equal(3, events.Count);
            }
        }
    }
}
