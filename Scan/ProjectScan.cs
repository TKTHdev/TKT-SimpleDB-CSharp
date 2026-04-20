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

    public bool HasField(string fldname)
    {
        return fieldlist.Contains(fldname);
    }

    public void Close()
    {
        _s.Close();
    }
}
