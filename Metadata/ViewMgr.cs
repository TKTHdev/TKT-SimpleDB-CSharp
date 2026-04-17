using DBSharp.Record;
using DBSharp.Transactions;

namespace DBSharp.Metadata;

public class ViewMgr
{
    private const int MAX_VIEWDEF = 100;
    private TableMgr _tblMgr;

    public ViewMgr(bool isNew, TableMgr tblMgr, Transaction tx)
    {
        _tblMgr = tblMgr;
        if (isNew)
        {
            var sch = new Schema();
            sch.AddStringField("viewname", TableMgr.MAX_NAME);
            sch.AddStringField("viewdef", MAX_VIEWDEF);
            tblMgr.CreateTable("viewcat", sch, tx);
        }
    }

    public void CreateView(string vname, string vdef, Transaction tx)
    {
        Layout layout = _tblMgr.GetLayout("viewcat", tx);
        var ts = new TableScan(tx, "viewcat", layout);
        ts.Insert();
        ts.SetString("viewname", vname);
        ts.SetString("viewdef", vdef);
        ts.Close();
    }

    public string? GetViewDef(string vname, Transaction tx)
    {
        string? result = null;
        Layout layout = _tblMgr.GetLayout("viewcat", tx);
        var ts = new TableScan(tx, "viewcat", layout);
        while (ts.Next())
        {
            if (ts.GetString("viewname").Equals(vname))
            {
                result = ts.GetString("viewdef");
                break;
            }
        }
        ts.Close();
        return result;
    }
}
