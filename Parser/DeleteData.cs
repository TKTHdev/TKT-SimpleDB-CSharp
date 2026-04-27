namespace DBSharp.Parser;

public class DeleteData
{
    private string _tblname;
    private Predicate.Predicate _pred;

    public DeleteData(string tblname, Predicate.Predicate pred)
    {
        _tblname = tblname;
        _pred = pred;
    }

    public string TableName() => _tblname;
    public Predicate.Predicate Pred() => _pred;
}