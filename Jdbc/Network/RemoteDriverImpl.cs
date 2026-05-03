namespace AyeAyeDB.Jdbc.Network;

public class RemoteDriverImpl : IRemoteDriver
{
    private readonly SimpleDB _db;

    public RemoteDriverImpl(SimpleDB db) => _db = db;

    public IRemoteConnection Connect() => new RemoteConnectionImpl(_db);
}
