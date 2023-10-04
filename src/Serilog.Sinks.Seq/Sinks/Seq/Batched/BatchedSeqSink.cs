// Copyright © Serilog Contributors
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;
using Serilog.Sinks.Seq.Http;

namespace Serilog.Sinks.Seq.Batched;

/// <summary>
/// The default Seq sink, for use in combination with <see cref="PeriodicBatchingSink"/>.
/// </summary>
sealed class BatchedSeqSink : IBatchedLogEventSink, IDisposable
{
    static readonly TimeSpan RequiredLevelCheckInterval = TimeSpan.FromMinutes(2);

    readonly ConstrainedBufferedFormatter _formatter;
    readonly SeqIngestionApi _ingestionApi;

    DateTime _nextRequiredLevelCheckUtc = DateTime.UtcNow.Add(RequiredLevelCheckInterval);
    readonly ControlledLevelSwitch _controlledSwitch;

    public BatchedSeqSink(
        SeqIngestionApi ingestionApi,
        long? eventBodyLimitBytes,
        ControlledLevelSwitch controlledSwitch)
    {
        _controlledSwitch = controlledSwitch ?? throw new ArgumentNullException(nameof(controlledSwitch));
        _formatter = new ConstrainedBufferedFormatter(eventBodyLimitBytes);
        _ingestionApi = ingestionApi ?? throw new ArgumentNullException(nameof(ingestionApi));
    }

    public void Dispose()
    {
        _ingestionApi.Dispose();
    }

    // The sink must emit at least one event on startup, and the server be
    // configured to set a specific level, before background level checks will be performed.
    public async Task OnEmptyBatchAsync()
    {
        if (_controlledSwitch.IsActive &&
            _nextRequiredLevelCheckUtc < DateTime.UtcNow)
        {
            await EmitBatchAsync(Enumerable.Empty<LogEvent>());
        }
    }

    public async Task EmitBatchAsync(IEnumerable<LogEvent> events)
    {
        _nextRequiredLevelCheckUtc = DateTime.UtcNow.Add(RequiredLevelCheckInterval);

        var payload = new StringWriter();
        foreach (var evt in events)
        {
            _formatter.Format(evt, payload);
        }

        var clefPayload = payload.ToString();

        var minimumAcceptedLevel = await _ingestionApi.IngestAsync(clefPayload);

        _controlledSwitch.Update(minimumAcceptedLevel);
    }
}