﻿using Serilog.Sinks.Seq.Tests.Support;
using Xunit;

namespace Serilog.Sinks.Seq.Tests;

public class ConstrainedBufferedFormatterTests
{
    [Fact]
    public void EventsAreFormattedIntoCompactJsonPayloads()
    {
        var evt = Some.LogEvent("Hello, {Name}!", "Alice");
        var formatter = new ConstrainedBufferedFormatter(null, new SeqCompactJsonFormatter());
        var json = new StringWriter();
        formatter.Format(evt, json);
        Assert.Contains("Name\":\"Alice", json.ToString());
    }

    [Fact]
    public void PlaceholdersAreLoggedWhenCompactJsonRenderingFails()
    {
        var evt = Some.LogEvent(new NastyException(), "Hello, {Name}!", "Alice");
        var formatter = new ConstrainedBufferedFormatter(null, new SeqCompactJsonFormatter());
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
        var formatter = new ConstrainedBufferedFormatter(2000, new SeqCompactJsonFormatter());
        var json = new StringWriter();
        formatter.Format(evt, json);
        var jsonString = json.ToString();
        Assert.Contains("exceeds the body size limit", jsonString);
        Assert.Contains("\"EventBodySample\"", jsonString);
        Assert.Contains("aaaaa", jsonString);
    }

    [Theory]
    [InlineData(0, 512)]
    [InlineData(1, 512)]
    [InlineData(512, 512)]
    [InlineData(1000, 512)]
    [InlineData(5000, 1476)]
    [InlineData(10000, 3976)]
    [InlineData(130048, 64000)]
    public void PlaceholderSampleSizeIsComputedFromEventBodyLimitBytes(long eventBodyLimitBytes, long expectedSampleSize)
    {
        var actual = ConstrainedBufferedFormatter.GetOversizeEventSampleLength(eventBodyLimitBytes);
        Assert.Equal(expectedSampleSize, actual);
    }
}