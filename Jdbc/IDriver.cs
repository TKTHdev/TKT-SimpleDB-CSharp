namespace DBSharp.Jdbc;

public interface IDriver
{
    IConnection Connect(string dbname);
}
