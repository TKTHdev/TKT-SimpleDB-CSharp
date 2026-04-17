using DBSharp.Record;
using DBSharp.Transactions;

namespace DBSharp.Metadata;

public class TableMgr
{
    public const int MAX_NAME = 16;
    private Layout _tcatLayout, _fcatLayout;

    public TableMgr(bool isNew, Transaction tx)
    {
        var tcatSchema = new Schema();
        tcatSchema.AddStringField("tblname", MAX_NAME);
        tcatSchema.AddIntField("slotsize");
        _tcatLayout = new Layout(tcatSchema);

        var fcatSchema = new Schema();
        fcatSchema.AddStringField("tblname", MAX_NAME);
        fcatSchema.AddStringField("fldname", MAX_NAME);
        fcatSchema.AddIntField("type");
        fcatSchema.AddIntField("length");
        fcatSchema.AddIntField("offset");
        _fcatLayout = new Layout(fcatSchema);

        if (isNew)
        {
            CreateTable("tblcat", tcatSchema, tx);
            CreateTable("fldcat", fcatSchema, tx);
        }
    }

    public void CreateTable(string tblname, Schema sch, Transaction tx)
    {
        var layout = new Layout(sch);
        // insert one record into tblcat
        var tcat = new TableScan(tx, "tblcat", _tcatLayout);
        tcat.Insert();
        tcat.SetString("tblname", tblname);
        tcat.SetInt("slotsize", layout.GetSlotSize());
        tcat.Close();

        // insert a record into fldcat for each field
        var fcat = new TableScan(tx, "fldcat", _fcatLayout);
        foreach (string fldname in sch.Fields())
        {
            fcat.Insert();
            fcat.SetString("tblname", tblname);
            fcat.SetString("fldname", fldname);
            fcat.SetInt("type", sch.Type(fldname));
            fcat.SetInt("length", sch.Length(fldname));
            fcat.SetInt("offset", layout.GetOffset(fldname));
        }
        fcat.Close();
    }

    public Layout GetLayout(string tblname, Transaction tx)
    {
        int size = -1;
        var tcat = new TableScan(tx, "tblcat", _tcatLayout);
        while (tcat.Next())
        {
            if (tcat.GetString("tblname").Equals(tblname))
            {
                size = tcat.GetInt("slotsize");
                break;
            }
        }
        tcat.Close();

        var sch = new Schema();
        var offsets = new Dictionary<string, int>();
        var fcat = new TableScan(tx, "fldcat", _fcatLayout);
        while (fcat.Next())
        {
            if (fcat.GetString("tblname").Equals(tblname))
            {
                string fldname = fcat.GetString("fldname");
                int fldtype = fcat.GetInt("type");
                int fldlen = fcat.GetInt("length");
                int offset = fcat.GetInt("offset");
                offsets[fldname] = offset;
                sch.AddField(fldname, fldtype, fldlen);
            }
        }
        fcat.Close();
        return new Layout(sch, offsets, size);
    }
}
