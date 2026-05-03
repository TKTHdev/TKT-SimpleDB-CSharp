namespace AyeAyeDB.Jdbc.Embedded;

public class EmbeddedStatement : StatementAdapter
{
    private readonly EmbeddedConnection _conn;
    private readonly Planner.Planner _planner;

    public EmbeddedStatement(EmbeddedConnection conn, Planner.Planner planner)
    {
        _conn = conn;
        _planner = planner;
    }

    public override IResultSet ExecuteQuery(string sql)
    {
        try
        {
            var tx = _conn.GetTransaction();
            var pln = _planner.CreateQueryPlan(sql, tx);
            return new EmbeddedResultSet(pln, _conn);
        }
        catch (Exception e)
        {
            _conn.Rollback();
            throw new InvalidOperationException(e.Message, e);
        }
    }

    public override int ExecuteUpdate(string sql)
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
