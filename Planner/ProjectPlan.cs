using DBSharp.Record;
using DBSharp.Scan;

namespace DBSharp.Planner;

public class ProjectPlan : IPlan
{
    private IPlan _p;
    private Schema _schema = new Schema();

    public ProjectPlan(IPlan p, List<string> fieldlist)
    {
        _p = p;
        foreach (string fldname in fieldlist)
            _schema.Add(fldname, _p.Schema());
    }

    public IScan Open()
    {
        IScan s = _p.Open();
        return new ProjectScan(s, _schema.Fields());
    }
    
    public int BlockAccessed() => _p.BlockAccessed();
      public int RecordsOutput() => _p.RecordsOutput();
    public int DistinctValues(string _fldname) => _p.DistinctValues(_fldname);
    public Schema Schema() => _schema;
}