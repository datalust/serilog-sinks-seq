using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.Seq.Tests.Support;

/// <summary>
/// A test enricher to add activity links to a log event.
///
/// These need to follow the SerilogTracing data model, which is <c>SequenceValue { Elements: IEnumerable{ScalarValue { Value: ActivityLink }} }</c>.
/// </summary>
public class SpanLinkEnricher: ILogEventEnricher
{
    private readonly SequenceValue _spanLinks;

    public SpanLinkEnricher(IEnumerable<ActivityLink> links)
    {
        _spanLinks = new SequenceValue([.. links.Select(l => new ScalarValue(l))]);
    }
    
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddOrUpdateProperty(new LogEventProperty("SpanLinks", _spanLinks));
    }
}