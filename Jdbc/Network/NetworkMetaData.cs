namespace DBSharp.Jdbc.Network;

public class NetworkMetaData : ResultSetMetaDataAdapter
{
    private readonly IRemoteMetaData _rmd;

    public NetworkMetaData(IRemoteMetaData rmd) => _rmd = rmd;

    public override int GetColumnCount()
    {
        try { return _rmd.GetColumnCount(); }
        catch (Exception e) { throw new InvalidOperationException(e.Message, e); }
    }

    public override string GetColumnName(int column)
    {
        try { return _rmd.GetColumnName(column); }
        catch (Exception e) { throw new InvalidOperationException(e.Message, e); }
    }

    public override int GetColumnType(int column)
    {
        try { return _rmd.GetColumnType(column); }
        catch (Exception e) { throw new InvalidOperationException(e.Message, e); }
    }

    public override int GetColumnDisplaySize(int column)
    {
        try { return _rmd.GetColumnDisplaySize(column); }
        catch (Exception e) { throw new InvalidOperationException(e.Message, e); }
    }
}
