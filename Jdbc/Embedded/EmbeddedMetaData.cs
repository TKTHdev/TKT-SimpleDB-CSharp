using DBSharp.Record;

namespace DBSharp.Jdbc.Embedded;

public class EmbeddedMetaData : ResultSetMetaDataAdapter
{
    private readonly Schema _sch;

    public EmbeddedMetaData(Schema sch) => _sch = sch;

    public override int GetColumnCount() => _sch.Fields().Count;

    public override string GetColumnName(int column) => _sch.Fields()[column - 1];

    public override int GetColumnType(int column)
    {
        string fldname = GetColumnName(column);
        return _sch.Type(fldname);
    }

    public override int GetColumnDisplaySize(int column)
    {
        string fldname = GetColumnName(column);
        int fldtype = _sch.Type(fldname);
        int fldlength = (fldtype == Schema.SqlType.INTEGER) ? 6 : _sch.Length(fldname);
        return Math.Max(fldname.Length, fldlength) + 1;
    }
}
