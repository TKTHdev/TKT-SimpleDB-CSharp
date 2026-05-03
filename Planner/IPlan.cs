namespace DBSharp.Planner;
using DBSharp.Scan;
using DBSharp.Record;

public interface Plan
{
    public IScan open();
    public int BlockAccessed();
    public int RecordsOutput();
    public int DistinctValues(string fldname);
    public Schema Schema();
}