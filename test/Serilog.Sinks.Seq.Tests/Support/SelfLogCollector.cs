using System;
using System.Collections.Generic;
using System.Threading;
using Serilog.Debugging;

namespace Serilog.Sinks.Seq.Tests.Support;

sealed class SelfLogCollector: IDisposable
{
    static readonly AsyncLocal<SelfLogCollector?> Collectors = new();

    static SelfLogCollector()
    {
        SelfLog.Enable(m => Collectors.Value?.Messages.Add(m));
    }

    public SelfLogCollector()
    {
        if (Collectors.Value != null)
            throw new InvalidOperationException("SelfLogCollector is already in use in this task.");

        Collectors.Value = this;
    }

    public IList<string> Messages { get; } = new List<string>();

    public void Dispose()
    {
        Collectors.Value = null;
    }
}