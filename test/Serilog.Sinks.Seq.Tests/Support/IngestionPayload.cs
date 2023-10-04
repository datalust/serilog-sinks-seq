namespace Serilog.Sinks.Seq.Tests.Support;

class IngestionPayload
{
    public IngestionPayload(string payload, string mediaType)
    {
        Payload = payload;
        MediaType = mediaType;
    }

    public string Payload { get; }
    public string MediaType { get; }

}