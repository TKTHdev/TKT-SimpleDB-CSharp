using AyeAyeDB.Record;
using AyeAyeDB.Scan;

namespace AyeAyeDB.Planner;

public class ProductPlan : IPlan
{
    private IPlan _p1, _p2;
    private Schema _schema = new Schema();

    public ProductPlan(IPlan p1, IPlan p2)
    {
        _p1 = p1;
        _p2 = p2;
        _schema.AddAll(p1.Schema());
        _schema.AddAll(p2.Schema());
    }

    public IScan Open()
    {
        IScan s1 = _p1.Open();
        IScan s2 = _p2.Open();
        return new ProductScan(s1, s2);
    }

    public int BlockAccessed() => _p1.BlockAccessed() + (_p1.RecordsOutput() * _p2.BlockAccessed());

    public int RecordsOutput() => _p1.RecordsOutput() * _p2.RecordsOutput();

    public int DistinctValues(string fldname)
    {
        if (_p1.Schema().HasField(fldname))
            return _p1.DistinctValues(fldname);
        else
            return _p2.DistinctValues(fldname);
    }

    public Schema Schema() => _schema;
}
