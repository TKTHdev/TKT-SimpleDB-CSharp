namespace DBSharp.Parser;

using DBSharp.Predicate;

public class ModifyData
{
    private string _tblname;
    private string _fldname;
    private Expression _newval;
    private Predicate _pred;

    public ModifyData(string tblname, string fldname, Expression newval, Predicate pred)
    {
        _tblname = tblname;
        _fldname = fldname;
        _newval = newval;
        _pred = pred;
    }

    public string TableName() => _tblname;
    public string TargetField() => _fldname;
    public Expression NewValue() => _newval;
    public Predicate Pred() => _pred;
}
