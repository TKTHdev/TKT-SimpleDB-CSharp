namespace DBSharp.Jdbc;

public interface IResultSetMetaData
{
    int GetColumnCount();
    string GetColumnName(int column);
    int GetColumnType(int column);
    int GetColumnDisplaySize(int column);
}
