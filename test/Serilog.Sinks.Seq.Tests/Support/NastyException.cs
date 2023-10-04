using System;

namespace Serilog.Sinks.Seq.Tests.Support;

public class NastyException : Exception
{
    public override string ToString()
    {
        throw new InvalidOperationException("Can't ToString() a NastyException!");
    }
}