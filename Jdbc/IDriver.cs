namespace AyeAyeDB.Jdbc;

public interface IDriver
{
    IConnection Connect(string dbname);
}
