namespace AyeAyeDB.Jdbc.Network;

public class NetworkStatement : StatementAdapter
{
    private readonly IRemoteStatement _rstmt;

    public NetworkStatement(IRemoteStatement rstmt) => _rstmt = rstmt;

    public override IResultSet ExecuteQuery(string sql)
    {
        try
        {
            return new NetworkResultSet(_rstmt.ExecuteQuery(sql));
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(e.Message, e);
        }
    }

    public override int ExecuteUpdate(string sql)
    {
        try
        {
            return _rstmt.ExecuteUpdate(sql);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(e.Message, e);
        }
    }
}
