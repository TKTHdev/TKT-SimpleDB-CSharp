namespace DBSharp.Jdbc.Network;

/// <summary>
/// Client-side JDBC Connection wrapping a RemoteConnection stub.
/// Translates IConnection calls to IRemoteConnection calls, converting
/// exceptions from the remote side to InvalidOperationException.
/// </summary>
public class NetworkConnection : ConnectionAdapter
{
    private readonly IRemoteConnection _rconn;

    public NetworkConnection(IRemoteConnection rconn) => _rconn = rconn;

    public override IStatement CreateStatement()
    {
        try
        {
            return new NetworkStatement(_rconn.CreateStatement());
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(e.Message, e);
        }
    }

    public override void Close()
    {
        try { _rconn.Close(); }
        catch (Exception e) { throw new InvalidOperationException(e.Message, e); }
    }

    public override void Commit() { }

    public override void Rollback() { }
}
