namespace AyeAyeDB.Jdbc;

public interface IResultSet
{
    bool Next();
    int GetInt(string fieldname);
    string GetString(string fieldname);
    IResultSetMetaData GetMetaData();
    void Close();
}
