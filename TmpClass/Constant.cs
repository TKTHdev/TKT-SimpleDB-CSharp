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
}
