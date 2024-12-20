﻿// Serilog.Sinks.Seq Copyright 2016 Serilog Contributors
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.Seq.Http;

namespace Serilog.Sinks.Seq.Audit;

/// <summary>
/// An <see cref="ILogEventSink"/> that synchronously propagates all <see cref="Emit"/> failures as exceptions.
/// </summary>
sealed class SeqAuditSink : ILogEventSink, IDisposable
{
    readonly SeqIngestionApi _ingestionApi;
    readonly ITextFormatter _payloadFormatter;

    public SeqAuditSink(SeqIngestionApi ingestionApi, ITextFormatter payloadFormatter)
    {
        _ingestionApi = ingestionApi ?? throw new ArgumentNullException(nameof(ingestionApi));
        _payloadFormatter = payloadFormatter ?? throw new ArgumentNullException(nameof(payloadFormatter));
    }

    public void Dispose()
    {
        _ingestionApi.Dispose();
    }

    public void Emit(LogEvent logEvent)
    {
        EmitAsync(logEvent).Wait();
    }

    async Task EmitAsync(LogEvent logEvent)
    {
        if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

        var payload = new StringWriter();
        _payloadFormatter.Format(logEvent, payload);

        await _ingestionApi.IngestAsync(payload.ToString()).ConfigureAwait(false);
    }
}
