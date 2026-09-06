namespace Serilog.Sinks.Seq.Tests.Support;

// Emulates the data model used by `SerilogTracing` for span links
public class Traceparent : IEquatable<Traceparent>, IComparable<Traceparent>, IComparable
{
    public Traceparent(string value)
    {
        _value = value;
    }
    
    private string _value;
    public string Value => _value;

    public override string ToString()
    {
        return Value;
    }

    public bool Equals(Traceparent? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Traceparent)obj);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public int CompareTo(Traceparent? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (other is null) return 1;
        return string.Compare(Value, other.Value, StringComparison.Ordinal);
    }

    public int CompareTo(object? obj)
    {
        if (obj is null) return 1;
        if (ReferenceEquals(this, obj)) return 0;
        return obj is Traceparent other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(Traceparent)}");
    }
}