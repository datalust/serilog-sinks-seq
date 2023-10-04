using Serilog.Events;
using Serilog.Sinks.Seq.Http;
using Xunit;

namespace Serilog.Sinks.Seq.Tests.Http;

public class SeqIngestionApiClientTests
{
    [Theory]
    [InlineData("https://example.com", "https://example.com/")]
    [InlineData("https://example.com/", "https://example.com/")]
    [InlineData("https://example.com:7777", "https://example.com:7777/")]
    [InlineData("https://example.com/test", "https://example.com/test/")]
    [InlineData("https://example.com/test/", "https://example.com/test/")]
    public void ServerBaseAddressesAreNormalized(string url, string expected)
    {
        var normalized = SeqIngestionApiClient.NormalizeServerBaseAddress(url);
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void MinimumAcceptedLevelIsExtractedWhenPresent()
    {
        const string response = @"{""MinimumLevelAccepted"":""Warning""}";
        var extracted = SeqIngestionApiClient.ReadIngestionResult(response);
        Assert.Equal(LogEventLevel.Warning, extracted);
    }
        
    [Fact]
    public void MinimumAcceptedLevelIsIgnoredWhenMissing()
    {
        const string response = @"{}";
        var extracted = SeqIngestionApiClient.ReadIngestionResult(response);
        Assert.Null(extracted);
    }
}