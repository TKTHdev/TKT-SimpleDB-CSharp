namespace DBSharp.Parser;

using DBSharp.Record;

public class CreateTableData
{
    private string _tblname;
    private Schema _sch;

    public CreateTableData(string tblname, Schema sch)
    {
        _tblname = tblname;
        _sch = sch;
    }

    public string TableName() => _tblname;
    public Schema NewSchema() => _sch;
}
