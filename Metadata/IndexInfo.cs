using DBSharp.Record;
using DBSharp.Transactions;

namespace DBSharp.Metadata;

public class IndexInfo
{
    private string _idxname, _fldname;
    private Transaction _tx;
    private Schema _tblSchema;
    private Layout _idxLayout;
    private StatInfo _si;

    public IndexInfo(string idxname, string fldname, Schema tblSchema,
                     Transaction tx, StatInfo si)
    {
        _idxname = idxname;
        _fldname = fldname;
        _tx = tx;
        _tblSchema = tblSchema;
        _idxLayout = CreateIdxLayout();
        _si = si;
    }

    public int BlocksAccessed()
    {
        int rpb = _tx.BlockSize() / _idxLayout.GetSlotSize();
        int numblocks = _si.RecordsOutput() / rpb;
        // HashIndex.searchCost(numblocks, rpb)
        // Since HashIndex is not yet implemented (Chapter 12),
        // use a simple estimate: numblocks / rpb
        return numblocks / rpb;
    }

    public int RecordsOutput()
    {
        return _si.RecordsOutput() / _si.DistinctValues(_fldname);
    }

    public int DistinctValues(string fname)
    {
        return _fldname.Equals(fname) ? 1 : _si.DistinctValues(_fldname);
    }

    private Layout CreateIdxLayout()
    {
        var sch = new Schema();
        sch.AddIntField("block");
        sch.AddIntField("id");
        if (_tblSchema.Type(_fldname) == Schema.SqlType.INTEGER)
            sch.AddIntField("dataval");
        else
        {
            int fldlen = _tblSchema.Length(_fldname);
            sch.AddStringField("dataval", fldlen);
        }
        return new Layout(sch);
    }
}
