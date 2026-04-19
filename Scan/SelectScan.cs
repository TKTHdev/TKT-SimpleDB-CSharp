namespace DBSharp.Scan;
using DBSharp.TmpClass;
using DBSharp.Record;



public class SelectScan : IUpdateScan
{
    private IScan _s;
    private Predicate _pred;

    public SelectScan(IScan s, Predicate p)
    {
        _s = s;
        _pred = _pred;
    }

    public void BeforeFirst()
    {
        _s.BeforeFirst();
    }

    public bool Next()
    {
        while(_s.Next())
            if(_pred.IsSatisfied(_s))
                return true;
        return false;
    }

    public int GetInt(string fldname)
    {
        return _s.GetInt(fldname);
    }

    public String GetString(string fldname)
    {
        return  _s.GetString(fldname);
    }

    public Constant GetVal(string fldname)
    {
        return _s.GetVal(fldname);
    }

    public bool HasField(string fldname)
    {
        return _s.HasField(fldname);
    }

    public void Close()
    {
        _s.Close();
    }
}