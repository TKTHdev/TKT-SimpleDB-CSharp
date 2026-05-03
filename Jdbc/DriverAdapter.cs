namespace AyeAyeDB.Jdbc;

public abstract class DriverAdapter : IDriver
{
    public virtual IConnection Connect(string dbname) =>
        throw new NotSupportedException("operation not implemented");
}
