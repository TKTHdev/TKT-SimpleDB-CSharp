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
}