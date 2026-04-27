namespace DBSharp.Parser;

using DBSharp.Predicate;

public class InsertData
{
    private string _tblname;
    private List<string> _flds;
    private List<Constant> _vals;

    public InsertData(string tblname, List<string> flds, List<Constant> vals)
    {
        _tblname = tblname;
        _flds = flds;
        _vals = vals;
    }

    public string TableName() => _tblname;
    public List<string> Fields() => _flds;
    public List<Constant> Vals() => _vals;
}
