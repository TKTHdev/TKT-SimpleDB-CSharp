namespace DBSharp.Jdbc.Network;

public class RemoteStatementImpl : IRemoteStatement
{
    private readonly RemoteConnectionImpl _conn;
    private readonly Planner.Planner _planner;

    public RemoteStatementImpl(RemoteConnectionImpl conn, Planner.Planner planner)
    {
        _conn = conn;
        _planner = planner;
    }

    public IRemoteResultSet ExecuteQuery(string sql)
    {
        try
        {
            var tx = _conn.GetTransaction();
            var pln = _planner.CreateQueryPlan(sql, tx);
            return new RemoteResultSetImpl(pln, _conn);
        }
        catch (Exception e)
        {
            _conn.Rollback();
            throw new InvalidOperationException(e.Message, e);
        }
    }

    public int ExecuteUpdate(string sql)
    {
        try
        {
            var tx = _conn.GetTransaction();
            int result = _planner.ExecuteUpdate(sql, tx);
            _conn.Commit();
            return result;
        }
        catch (Exception e)
        {
            _conn.Rollback();
            throw new InvalidOperationException(e.Message, e);
        }
    }
}
