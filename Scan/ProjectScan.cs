namespace DBSharp.Scan;
using DBSharp.TmpClass;

public class ProjectScan : IScan
{
    private IScan _s;
    private ICollection<string> fieldlist;

    public ProjectScan(IScan scan, List<string> fieldlist)
    {
        _s = scan;
        this.fieldlist = fieldlist;
    }

    public void BeforeFirst()
    {
        _s.BeforeFirst();
    }

    public bool Next()
    {
        return _s.Next();
    }

    public int GetInt(string fldname)
    {
        if(HasField(fldname))
            return _s.GetInt(fldname);
        else
            throw new Exception("Field not found");
    }

    public string GetString(string fldname)
    {
         if(HasField(fldname))
            return _s.GetString(fldname);
         else
            throw new Exception("Field not found");
    }

    public Constant GetVal(string fldname)
    {
        if(HasField(fldname))
            return _s.GetVal(fldname);
        else
            throw new Exception("Field not found");
    }

    public bool HasField(string fldname)
    {
        return fieldlist.Contains(fldname);
    }

    public void Close()
    {
        _s.Close();
    }
}
