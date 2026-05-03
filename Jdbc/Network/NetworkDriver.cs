namespace AyeAyeDB.Jdbc.Network;

/// <summary>
/// Client-side JDBC Driver that connects to a SimpleDbServer over TCP.
/// Wraps the remote connection stub (equivalent to Java's RMI stub).
/// The host is provided as the dbname argument (e.g. "localhost" or "192.168.1.1").
/// </summary>
public class NetworkDriver : DriverAdapter
{
    private readonly int _port;

    public NetworkDriver(int port = SimpleDbServer.DefaultPort) => _port = port;

    public override IConnection Connect(string host)
    {
        try
        {
            var rconn = new RemoteConnectionStub(host, _port);
            return new NetworkConnection(rconn);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(e.Message, e);
        }
    }
}
