using DBSharp.TmpClass;

namespace DBSharp.Scan;

public class ProductScan : IScan
{
    private IScan _s1, _s2;

    public ProductScan(IScan s1, IScan s2)
    {
        _s1 = s1;
        _s2 = s2;
        _s1.Next();
    }

    public void BeforeFirst()
    {
        _s1.BeforeFirst();
        _s1.Next();
        _s2.BeforeFirst();
    }

    public bool Next()
    {
        if (_s2.Next())
            return true;
        else
        {
            _s2.BeforeFirst();
            return _s2.Next() && _s1.Next();
        }
    }

    public int GetInt(string fldname)
    {
        if (_s1.HasField(fldname))
            return _s1.GetInt(fldname);
        else
            return _s2.GetInt(fldname);
    }

    public string GetString(string fldname)
    {
        if (_s1.HasField(fldname))
            return _s1.GetString(fldname);
        else
            return _s2.GetString(fldname);
    }

    public Constant GetVal(string fldname)
    {
        if (_s1.HasField(fldname))
            return _s1.GetVal(fldname);
        else
            return _s2.GetVal(fldname);
    }

    public bool HasField(string fldname)
    {
        return  _s1.HasField(fldname) ||  _s2.HasField(fldname);
    }
}
