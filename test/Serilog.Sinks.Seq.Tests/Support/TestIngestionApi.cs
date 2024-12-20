using System.Net;
using System.Threading.Channels;
using Serilog.Sinks.Seq.Http;

namespace Serilog.Sinks.Seq.Tests.Support;

class TestIngestionApi : SeqIngestionApi
{
    readonly Func<IngestionPayload, Task<IngestionResult>>? _onIngestAsync;
    readonly Channel<IngestionPayload> _ingested = Channel.CreateBounded<IngestionPayload>(100);

    public ChannelReader<IngestionPayload> Ingested => _ingested.Reader;
    public bool IsDisposed { get; set; }
        
    public TestIngestionApi(Func<IngestionPayload, Task<IngestionResult>>? onIngestAsync = null)
    {
        _onIngestAsync = onIngestAsync;
    }
        
    public override async Task<IngestionResult> TryIngestAsync(string payload, string mediaType)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(TestIngestionApi));
            
        var ingestionPayload = new IngestionPayload(payload, mediaType);

        IngestionResult result;
        if (_onIngestAsync != null)
        {
            result = await _onIngestAsync(ingestionPayload);
        }
        else
        {
            result = new IngestionResult(true, HttpStatusCode.Accepted, null);
        }

        if (result.Succeeded)
        {
            if (!_ingested.Writer.TryWrite(ingestionPayload))
                throw new InvalidOperationException("Channel capacity exceeded.");
        }

        return result;
    }

    public async Task<IngestionPayload> GetPayloadAsync(TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(10));
        return await _ingested.Reader.ReadAsync(cts.Token);
    }

    public override void Dispose()
    {
        IsDisposed = true;;
        base.Dispose();
    }
}