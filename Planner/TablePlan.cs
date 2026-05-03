using AyeAyeDB.Metadata;
using AyeAyeDB.Record;
using AyeAyeDB.Scan;
using AyeAyeDB.Transactions;

namespace AyeAyeDB.Planner;

public class TablePlan : IPlan
{
    private Transaction _tx;
    private string _tblname;
    private Layout _layout;
    private StatInfo _si;

    public TablePlan(string tblname,Transaction tx, MetadataMgr md)
    {
        _tx = tx;
        _tblname = tblname;
        _layout = md.GetLayout(tblname, tx);
        _si = md.GetStatInfo(tblname, _layout, tx);
    }

    public IScan Open() =>new TableScan(_tx, _tblname, _layout);
    public int BlockAccessed() => _si.BlocksAccessed();
    public int RecordsOutput() => _si.RecordsOutput();
    public int DistinctValues(string _fldname) => _si.DistinctValues(_fldname);
    public Schema Schema() => _layout.GetSchema();
    
}