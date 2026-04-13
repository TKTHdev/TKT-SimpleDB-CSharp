namespace DBSharp.TmpClass;

public class Constant : IComparable<Constant>, IEquatable<Constant>
{
    private readonly int? _ival;
    private readonly string? _sval;

    public Constant(int ival)
    {
        _ival = ival;
    }

    public Constant(string sval)
    {
        _sval = sval;
    }

    public int AsInt()
    {
        return _ival!.Value;
    }

    public string AsString()
    {
        return _sval!;
    }

    public bool Equals(Constant? other)
    {
        if (other is null) return false;
        return _ival.HasValue
            ? _ival.Value.Equals(other._ival)
            : _sval!.Equals(other._sval);
    }

    public override bool Equals(object? obj)
    {
        return obj is Constant c && Equals(c);
    }

    public override int GetHashCode()
    {
        return _ival.HasValue ? _ival.Value.GetHashCode() : _sval!.GetHashCode();
    }
}
