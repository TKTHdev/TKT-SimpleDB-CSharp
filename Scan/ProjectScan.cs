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
}
