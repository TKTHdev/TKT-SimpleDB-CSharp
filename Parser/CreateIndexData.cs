namespace DBSharp.Parser;

public class CreateIndexData
{
    private string _idxname;
    private string _tblname;
    private string _fldname;

    public CreateIndexData(string idxname, string tblname, string fldname)
    {
        _idxname = idxname;
        _tblname = tblname;
        _fldname = fldname;
    }

    public string IndexName() => _idxname;
    public string TableName() => _tblname;
    public string FieldName() => _fldname;
}
