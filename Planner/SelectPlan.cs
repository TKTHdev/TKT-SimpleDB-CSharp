using AyeAyeDB.Record;
using AyeAyeDB.Scan;

namespace AyeAyeDB.Planner;
using AyeAyeDB.Predicate;

public class SelectPlan : IPlan
{
    private IPlan _p;
    private Predicate _pred;

    public SelectPlan(IPlan p, Predicate pred)
    {
        _p = p;
        _pred = pred;
    }

    public IScan Open()
    {
        IScan s = _p.Open();
        return new SelectScan(s, _pred);
    }

    public int BlockAccessed() => _p.BlockAccessed();
    public int RecordsOutput() => _p.RecordsOutput() / _pred.ReductionFactor(_p);

    public int DistinctValues(string fldname)
    {
        if (_pred.EquatesWithConstant(fldname) != null)
            return 1;
        else
        {
            string fldname2 = _pred.EquatesWithField(fldname);
            if (fldname2 != null)
                return Math.Min(_p.DistinctValues(fldname), _p.DistinctValues(fldname2));
            else 
                return _p.DistinctValues(fldname);
        }
    }

    public Schema Schema() => _p.Schema();
}