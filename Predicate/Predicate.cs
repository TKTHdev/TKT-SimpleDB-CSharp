namespace DBSharp.Predicate;
using DBSharp.Scan;
using DBSharp.Planner;
using DBSharp.Record;

public class Predicate
{
    private List<Term> _terms = new();

    public Predicate() { }
    public Predicate(Term t) => _terms.Add(t);

    public void ConjoinWith(Predicate pred)
    {
        _terms.AddRange(pred._terms);
    }

    public bool IsSatisfied(IScan s)
    {
        foreach (Term t in _terms)
            if (!t.IsSatisfied(s))
                return false;
        return true;
    }

    public int ReductionFactor(IPlan p)
    {
        throw new NotImplementedException();
    }

    public Predicate SelectSubPred(Schema sch)
    {
        throw new NotImplementedException();
    }

    public Predicate JoinSubPred(Schema sch)
    {
        throw new NotImplementedException();
    }

    public Constant EquatesWithConstant(string fldname)
    {
        foreach (Term t in _terms)
        {
            Constant c = t.EquatesWithConstant(fldname);
            if(c!=null)
                return c;
        }

        return null;
    }

    public string EquatesWithField(string fldname)
    {
        foreach (Term t in _terms)
        {
            string s = t.EquatesWithField(fldname);
            if (s != null)
                return s;
        }

        return null;
    }

    public override string ToString()
    {
        return string.Join(" and ", _terms);
    }
}