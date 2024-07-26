// This file originally CompactJsonFormatterTests from https://github.com/serilog/serilog-formatting-compact,
// Copyright Serilog Contributors and distributed under the Apache 2.0 license.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;
// ReSharper disable AccessToDisposedClosure
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace Serilog.Sinks.Seq.Tests;

public class SeqCompactJsonFormatterTests
{
    JObject AssertValidJson(Action<ILogger> act, IFormatProvider? formatProvider = null, Action<JObject>? assert = null)
    {
        var sw = new StringWriter();
        var logger = new LoggerConfiguration()
            .Destructure.AsScalar<ActivityTraceId>()
            .Destructure.AsScalar<ActivitySpanId>()
            .WriteTo.TextWriter(new SeqCompactJsonFormatter(formatProvider), sw)
            .CreateLogger();
        act(logger);
        logger.Dispose();
        var json = sw.ToString();

        var settings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None,
            CheckAdditionalContent = true,
        };

        var evt = JsonConvert.DeserializeObject<JObject>(json, settings)!;
        (assert ?? (_ => { }))(evt);
        return evt;
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    public void PropertiesFormatCorrectlyForTheFormatProvider(string cultureName)
    {
        var cultureInfo = new CultureInfo(cultureName);
        
        var number = Math.PI * 10000;
        var date = new DateTime(2024, 7, 19, 10, 00, 59);
        var currency = 12345.67M;

        // Culture-specific formatting differs by .NET version (and possibly OS).
        var messageBuilder = new StringWriter(cultureInfo);
        messageBuilder.Write("{0:n} {1} {2:C}", number, date, currency);
        var expectedMessage = messageBuilder.ToString();
        
        AssertValidJson(log => log.Information("{a:n} {b} {c:C}", number, date, currency), cultureInfo, evt =>
        {
            Assert.Equal(expectedMessage, evt["@m"]!.Value<string>());
            Assert.Null(evt["@r"]);
        });
    }

    [Fact]
    public void MessageNotRenderedForDefaultFormatProvider()
    {
        AssertValidJson(log => log.Information("{a}", 1.234), null, evt =>
        {
            Assert.Null(evt["@m"]);
        });
    }

    [Fact]
    public void MessageNotRenderedForExplicitInvariantCulture()
    {
        AssertValidJson(log => log.Information("{a}", 1.234), CultureInfo.InvariantCulture, evt =>
        {
            Assert.Null(evt["@m"]);
        });
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

    [Fact]
    public void AtPrefixedPropertyNamesAreEscaped()
    {
        // Not possible in message templates, but accepted this way
        var jObject = AssertValidJson(log => log.ForContext("@Mistake", 42)
                                                .Information("Hello"));

        Assert.True(jObject.TryGetValue("@@Mistake", out var val));
        Assert.Equal(42, val.ToObject<int>());
    }

    [Fact]
    public void TimestampIsUtc()
    {
        // Not possible in message templates, but accepted this way
        var jObject = AssertValidJson(log => log.Information("Hello"));

        Assert.True(jObject.TryGetValue("@t", out var val));
        Assert.EndsWith("Z", val.ToObject<string>());
    }

    [Fact]
    public void TraceAndSpanIdsGenerateValidJson()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var evt = new LogEvent(DateTimeOffset.Now, LogEventLevel.Information, null,
            new MessageTemplate(Enumerable.Empty<MessageTemplateToken>()), Enumerable.Empty<LogEventProperty>(),
            traceId, spanId);
        var json = AssertValidJson(log => log.Write(evt));
        Assert.Equal(traceId.ToHexString(), json["@tr"]);
        Assert.Equal(spanId.ToHexString(), json["@sp"]);
    }
    
    [Fact]
    public void RecognizesSerilogTracingProperties()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        
        using var source = new ActivitySource(nameof(SeqCompactJsonFormatterTests));
        
        using var listener = new ActivityListener();
        listener.ShouldListenTo = s => s.Name == source.Name;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        using var parent = source.StartActivity();
        Assert.NotNull(parent);

        using var child = source.StartActivity();
        Assert.NotNull(child);
        
        var st = DateTime.UtcNow;
        var tr = child.TraceId;
        var sp = child.SpanId;
        var ps = parent.SpanId;
        const ActivityKind sk = ActivityKind.Server;
        
        var jObject = AssertValidJson(log => log.Information("{SpanStartTimestamp} {ParentSpanId} {SpanKind}", st, ps, sk));

        Assert.False(jObject.ContainsKey("SpanStartTimestamp"));

        Assert.True(jObject.TryGetValue("@st", out var stValue));
        Assert.Equal(st.ToString("o"), stValue.ToObject<string>());

        Assert.True(jObject.TryGetValue("@tr", out var trValue));
        Assert.Equal(tr.ToHexString(), trValue.ToObject<string>());

        Assert.True(jObject.TryGetValue("@sp", out var spValue));
        Assert.Equal(sp.ToHexString(), spValue.ToObject<string>());

        Assert.True(jObject.TryGetValue("@ps", out var psValue));
        Assert.Equal(ps.ToHexString(), psValue.ToObject<string>());

        Assert.True(jObject.TryGetValue("@sk", out var skValue));
        Assert.Equal("Server", skValue.ToObject<string>());
    }
    
    [Fact]
    public void IgnoresSerilogTracingPropertiesWhenNotTracing()
    {
        var st = DateTime.UtcNow;
        var jObject = AssertValidJson(log => log.Information("{SpanStartTimestamp}", st));

        Assert.True(jObject.ContainsKey("SpanStartTimestamp"));
        
        Assert.False(jObject.ContainsKey("@st"));
    }
}