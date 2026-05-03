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
        if (_lhs.IsFieldName() &&
            _lhs.AsFieldName().Equals(fldname) &&
            !_rhs.IsFieldName()
           )
            return _rhs.AsConstant();
        else if (_rhs.IsFieldName() &&
                 _rhs.AsFieldName().Equals(fldname) &&
                 !_lhs.IsFieldName())
            return _lhs.AsConstant();
        else
            return null;
    }

    public string EquatesWithField(string fldname)
    {
        if (_lhs.IsFieldName() &&
            _lhs.AsFieldName().Equals(fldname) &&
            _rhs.IsFieldName())
            return _rhs.AsFieldName();
        else if (_rhs.IsFieldName() &&
                 _rhs.AsFieldName().Equals(fldname) &&
                 _lhs.IsFieldName())
            return _lhs.AsFieldName();
        else
            return null;
    }

    public override string ToString()
    {
        return _lhs.ToString() + "=" + _rhs.ToString();
    }

}