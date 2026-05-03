namespace DBSharp.Jdbc;

public interface IConnection
{
    IStatement CreateStatement();
    void Close();
    void Commit();
    void Rollback();
}
