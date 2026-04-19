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

    public void SetInt(string fldname, int value)
    {
        IUpdateScan us  = _s as IUpdateScan;
        us.SetInt(fldname, value);
    }

    public void SetString(string fldname, string value)
    {
        IUpdateScan us  = _s as IUpdateScan;
        us.SetString(fldname, value);
    }

    public void SetVal(string fldname, Constant value)
    {
        IUpdateScan us  = _s as IUpdateScan;
        us.SetVal(fldname, value);
    }

    public void Delete()
    {
        IUpdateScan us  = _s as IUpdateScan;
        us.Delete();
    }

    public void Insert()
    {
        IUpdateScan us  = _s as IUpdateScan;
        us.Insert();
    }

    public RID GetRid()
    {
        IUpdateScan us  = _s as IUpdateScan;
        return us.GetRid();
    }

    public void MoveToRid(RID rid)
    {
        IUpdateScan us  = _s as IUpdateScan;
        us.MoveToRid(rid);
    }
}