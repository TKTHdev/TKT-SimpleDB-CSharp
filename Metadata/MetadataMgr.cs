using DBSharp.Record;
using DBSharp.Transactions;

namespace DBSharp.Metadata;

public class MetadataMgr
{
    private static TableMgr _tblMgr = null!;
    private static ViewMgr _viewMgr = null!;
    private static StatMgr _statMgr = null!;
    private static IndexMgr _idxMgr = null!;

    public MetadataMgr(bool isNew, Transaction tx)
    {
        _tblMgr = new TableMgr(isNew, tx);
        _viewMgr = new ViewMgr(isNew, _tblMgr, tx);
        _statMgr = new StatMgr(_tblMgr, tx);
        _idxMgr = new IndexMgr(isNew, _tblMgr, _statMgr, tx);
    }

    public void CreateTable(string tblname, Schema sch, Transaction tx)
    {
        _tblMgr.CreateTable(tblname, sch, tx);
    }

    public Layout GetLayout(string tblname, Transaction tx)
    {
        return _tblMgr.GetLayout(tblname, tx);
    }

    public void CreateView(string viewname, string viewdef, Transaction tx)
    {
        _viewMgr.CreateView(viewname, viewdef, tx);
    }

    public string? GetViewDef(string viewname, Transaction tx)
    {
        return _viewMgr.GetViewDef(viewname, tx);
    }

    public void CreateIndex(string idxname, string tblname,
                            string fldname, Transaction tx)
    {
        _idxMgr.CreateIndex(idxname, tblname, fldname, tx);
    }

    public Dictionary<string, IndexInfo> GetIndexInfo(string tblname,
                                                      Transaction tx)
    {
        return _idxMgr.GetIndexInfo(tblname, tx);
    }

    public StatInfo GetStatInfo(string tblname, Layout layout,
                                Transaction tx)
    {
        return _statMgr.GetStatInfo(tblname, layout, tx);
    }
}
