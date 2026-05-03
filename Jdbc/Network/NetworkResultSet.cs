namespace DBSharp.Jdbc.Network;

public class NetworkResultSet : ResultSetAdapter
{
    private readonly IRemoteResultSet _rrs;

    public NetworkResultSet(IRemoteResultSet rrs) => _rrs = rrs;

    public override bool Next()
    {
        try { return _rrs.Next(); }
        catch (Exception e) { throw new InvalidOperationException(e.Message, e); }
    }

    public override int GetInt(string fieldname)
    {
        try { return _rrs.GetInt(fieldname); }
        catch (Exception e) { throw new InvalidOperationException(e.Message, e); }
    }

    public override string GetString(string fieldname)
    {
        try { return _rrs.GetString(fieldname); }
        catch (Exception e) { throw new InvalidOperationException(e.Message, e); }
    }

    public override IResultSetMetaData GetMetaData()
    {
        try { return new NetworkMetaData(_rrs.GetMetaData()); }
        catch (Exception e) { throw new InvalidOperationException(e.Message, e); }
    }

    public override void Close()
    {
        try { _rrs.Close(); }
        catch (Exception e) { throw new InvalidOperationException(e.Message, e); }
    }
}
