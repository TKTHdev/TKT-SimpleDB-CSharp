namespace AyeAyeDB.Jdbc.Network;

internal class RemoteStatementStub : IRemoteStatement
{
    private readonly TcpSession _session;

    public RemoteStatementStub(TcpSession session) => _session = session;

    public IRemoteResultSet ExecuteQuery(string sql)
    {
        string response = _session.SendQuery(sql);
        if (response != "OK")
            throw new InvalidOperationException("query failed: " + response);
        return new RemoteResultSetStub(_session);
    }

    public int ExecuteUpdate(string sql) => _session.SendUpdate(sql);
}
