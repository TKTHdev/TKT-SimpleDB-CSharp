namespace DBSharp.Predicate;
using DBSharp.Scan;
using DBSharp.Record;
using DBSharp.Planner;

public class Term
{
    private Expression _lhs, _rhs;

    public Term(Expression lhs, Expression rhs)
    {
        this._lhs = lhs;
        this._rhs = rhs;
    }

    public bool IsSatisfied(IScan s)
    {
        Constant lhsval = _lhs.Evaluate(s);
        Constant rhsval = _rhs.Evaluate(s);
        return rhsval.Equals(lhsval);
    }

    public bool AppliesTo(Schema sch)
    {
        return _lhs.AppliesTo(sch) && _rhs.AppliesTo(sch);
    }
    public int ReductionFactor(Plan p)
    {
        throw new NotImplementedException();
    }

    public Constant EquatesWithConstant(string fldname)
    {
        throw new NotImplementedException();
    }

    public string EquatesWithField(string fldname)
    {
        throw new NotImplementedException();
    }

    public override string ToString()
    {
        return _lhs.ToString() + "=" + _rhs.ToString();
    }

}