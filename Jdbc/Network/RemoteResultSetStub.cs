namespace AyeAyeDB.Jdbc.Network;

internal class RemoteResultSetStub : IRemoteResultSet
{
    private readonly TcpSession _session;

    public RemoteResultSetStub(TcpSession session) => _session = session;

    public bool Next() => _session.Next();

    public int GetInt(string fieldname) => _session.GetInt(fieldname);

    public string GetString(string fieldname) => _session.GetString(fieldname);

    public IRemoteMetaData GetMetaData()
    {
        var (count, cols) = _session.GetMetaData();
        return new RemoteMetaDataStub(count, cols);
    }

    public void Close() => _session.CloseResultSet();
}
