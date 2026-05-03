namespace DBSharp.Jdbc.Network;

/// <summary>
/// Client-side stub for IRemoteMetaData. Holds metadata fetched once from the server.
/// </summary>
internal class RemoteMetaDataStub : IRemoteMetaData
{
    private readonly List<(string name, int type, int displaySize)> _cols;

    public RemoteMetaDataStub(int count, List<(string name, int type, int displaySize)> cols)
    {
        _cols = cols;
    }

    public int GetColumnCount() => _cols.Count;

    public string GetColumnName(int column) => _cols[column - 1].name;

    public int GetColumnType(int column) => _cols[column - 1].type;

    public int GetColumnDisplaySize(int column) => _cols[column - 1].displaySize;
}
