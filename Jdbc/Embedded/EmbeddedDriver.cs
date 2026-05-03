namespace AyeAyeDB.Jdbc.Embedded;

public class EmbeddedDriver : DriverAdapter
{
    public override IConnection Connect(string dbname)
    {
        var db = new SimpleDB(dbname);
        return new EmbeddedConnection(db);
    }
}
