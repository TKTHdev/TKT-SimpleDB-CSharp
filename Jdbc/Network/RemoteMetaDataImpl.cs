using AyeAyeDB.Record;

namespace AyeAyeDB.Jdbc.Network;

public class RemoteMetaDataImpl : IRemoteMetaData
{
    private readonly Schema _sch;

    public RemoteMetaDataImpl(Schema sch) => _sch = sch;

    public int GetColumnCount() => _sch.Fields().Count;

    public string GetColumnName(int column) => _sch.Fields()[column - 1];

    public int GetColumnType(int column) => _sch.Type(GetColumnName(column));

    public int GetColumnDisplaySize(int column)
    {
        string fldname = GetColumnName(column);
        int fldtype = _sch.Type(fldname);
        int fldlength = (fldtype == Schema.SqlType.INTEGER) ? 6 : _sch.Length(fldname);
        return Math.Max(fldname.Length, fldlength) + 1;
    }
}
