namespace AyeAyeDB.Jdbc;

public interface IStatement
{
    IResultSet ExecuteQuery(string sql);
    int ExecuteUpdate(string sql);
    void Close();
}
