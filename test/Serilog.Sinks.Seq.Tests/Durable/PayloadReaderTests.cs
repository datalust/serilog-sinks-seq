using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Serilog.Sinks.Seq.Durable;
using Serilog.Sinks.Seq.Http;
using Serilog.Sinks.Seq.Tests.Support;
using Xunit;
using IOFile = System.IO.File;

namespace Serilog.Sinks.Seq.Tests.Durable;

public class PayloadReaderTests
{
    [Fact]
    public void ReadsEventsFromBufferFiles()
    {
        using var tmp = new TempFolder();
        var fn = tmp.AllocateFilename("clef");
        var lines = IOFile.ReadAllText(Path.Combine("Resources", "ThreeBufferedEvents.clef.txt"), Encoding.UTF8)
            // ReSharper disable once RedundantCast
            .Split((char[])['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        using (var f = IOFile.Create(fn))
        using (var fw = new StreamWriter(f, Encoding.UTF8))
        {
            foreach (var line in lines)
            {
                fw.WriteLine(line);
            }
        }
        var position = new FileSetPosition(0, fn);
        var count = 0;
        PayloadReader.ReadPayload(1000, null, ref position, ref count, out var mimeType);
                
        Assert.Equal(SeqIngestionApi.CompactLogEventFormatMediaType, mimeType);

        Assert.Equal(3, count);
        Assert.Equal(465 + 3 * (Environment.NewLine.Length - 1), position.NextLineStart);
        Assert.Equal(fn, position.File);
    }

    [Fact]
    public void ReadsEventsFromRawBufferFiles()
    {
        using var tmp = new TempFolder();
        var fn = tmp.AllocateFilename("json");
        var lines = IOFile.ReadAllText(Path.Combine("Resources", "ThreeBufferedEvents.json.txt"), Encoding.UTF8)
            // ReSharper disable once RedundantCast
            .Split((char[])['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        using (var f = IOFile.Create(fn))
        using (var fw = new StreamWriter(f, Encoding.UTF8))
        {
            foreach (var line in lines)
            {
                fw.WriteLine(line);
            }
        }
        var position = new FileSetPosition(0, fn);
        var count = 0;
        var payload = PayloadReader.ReadPayload(1000, null, ref position, ref count, out var mimeType);
                
        Assert.Equal(SeqIngestionApi.RawEventFormatMediaType, mimeType);

        Assert.Equal(3, count);
        Assert.Equal(576 + 3 * (Environment.NewLine.Length - 1), position.NextLineStart);
        Assert.Equal(fn, position.File);

        var data = JsonConvert.DeserializeObject<dynamic>(payload)!;
        var events = data["Events"];
        Assert.NotNull(events);
        Assert.Equal(3, events.Count);
    }
}