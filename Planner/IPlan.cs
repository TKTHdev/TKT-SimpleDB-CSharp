namespace AyeAyeDB.Planner;
using AyeAyeDB.Scan;
using AyeAyeDB.Record;

public interface IPlan
{
    public IScan Open();
    public int BlockAccessed();
    public int RecordsOutput();
    public int DistinctValues(string fldname);
    public Schema Schema();
}