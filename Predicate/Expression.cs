namespace DBSharp.Predicate;
using DBSharp.Scan;
using DBSharp.Record;

public class Expression
{
    private Constant val = null;
    private string fldname = null;

    public Expression(Constant val)
    {
        this.val = val;
    }

    public Expression(string fldname)
    {
        this.fldname = fldname;
    }

    public bool IsFieldName()
    {
        return fldname != null;
    }

    public Constant AsConstant()
    {
        return val;
    }

    public string AsFieldName()
    {
        return fldname;
    }

    public Constant Evaluate(IScan s)
    {
        return (val != null) ? val : s.GetVal(fldname);
    }

    public bool AppliesTo(Schema sch)
    {
        return (val != null) ? true : sch.HasField(fldname);
    }

    public String ToString()
    {
        return (val != null) ? val.ToString() : fldname;
    }

}