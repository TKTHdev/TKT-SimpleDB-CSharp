namespace DBSharp.Jdbc.Network;

/// <summary>
/// Client-side stub for IRemoteConnection.
/// Communicates with the server via TcpSession (equivalent to a Java RMI stub).
/// </summary>
internal class RemoteConnectionStub : IRemoteConnection
{
    private readonly TcpSession _session;

    public RemoteConnectionStub(string host, int port)
    {
        _session = new TcpSession(host, port);
    }

    public IRemoteStatement CreateStatement() =>
        new RemoteStatementStub(_session);

    public void Close()
    {
        _session.CloseConnection();
        _session.Dispose();
    }

    internal TcpSession Session => _session;
}
