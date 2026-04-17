using DBSharp.Record;
using DBSharp.Transactions;

namespace DBSharp.Metadata;

public class IndexMgr
{
    private Layout _layout;
    private TableMgr _tblMgr;
    private StatMgr _statMgr;

    public IndexMgr(bool isNew, TableMgr tblMgr, StatMgr statMgr,
                    Transaction tx)
    {
        if (isNew)
        {
            var sch = new Schema();
            sch.AddStringField("indexname", TableMgr.MAX_NAME);
            sch.AddStringField("tablename", TableMgr.MAX_NAME);
            sch.AddStringField("fieldname", TableMgr.MAX_NAME);
            tblMgr.CreateTable("idxcat", sch, tx);
        }
        _tblMgr = tblMgr;
        _statMgr = statMgr;
        _layout = tblMgr.GetLayout("idxcat", tx);
    }

    public void CreateIndex(string idxname, string tblname,
                            string fldname, Transaction tx)
    {
        var ts = new TableScan(tx, "idxcat", _layout);
        ts.Insert();
        ts.SetString("indexname", idxname);
        ts.SetString("tablename", tblname);
        ts.SetString("fieldname", fldname);
        ts.Close();
    }

    public Dictionary<string, IndexInfo> GetIndexInfo(string tblname,
                                                      Transaction tx)
    {
        var result = new Dictionary<string, IndexInfo>();
        var ts = new TableScan(tx, "idxcat", _layout);
        while (ts.Next())
        {
            if (ts.GetString("tablename").Equals(tblname))
            {
                string idxname = ts.GetString("indexname");
                string fldname = ts.GetString("fieldname");
                Layout tblLayout = _tblMgr.GetLayout(tblname, tx);
                StatInfo tblsi = _statMgr.GetStatInfo(tblname, tblLayout, tx);
                IndexInfo ii = new IndexInfo(idxname, fldname,
                                             tblLayout.GetSchema(), tx, tblsi);
                result[fldname] = ii;
            }
        }
        ts.Close();
        return result;
    }
}
