using DBSharp.Planner;
using DBSharp.Record;
using DBSharp.Scan;

namespace DBSharp.Jdbc.Embedded;

public class EmbeddedResultSet : ResultSetAdapter
{
    private readonly IScan _scan;
    private readonly Schema _sch;
    private readonly EmbeddedConnection _conn;

    public EmbeddedResultSet(IPlan plan, EmbeddedConnection conn)
    {
        _scan = plan.Open();
        _sch = plan.Schema();
        _conn = conn;
    }

    public override bool Next()
    {
        try
        {
            return _scan.Next();
        }
        catch (Exception e)
        {
            _conn.Rollback();
            throw new InvalidOperationException(e.Message, e);
        }
    }

    public override int GetInt(string fieldname)
    {
        try
        {
            return _scan.GetInt(fieldname.ToLower());
        }
        catch (Exception e)
        {
            _conn.Rollback();
            throw new InvalidOperationException(e.Message, e);
        }
    }

    public override string GetString(string fieldname)
    {
        try
        {
            return _scan.GetString(fieldname.ToLower());
        }
        catch (Exception e)
        {
            _conn.Rollback();
            throw new InvalidOperationException(e.Message, e);
        }
    }

    public override IResultSetMetaData GetMetaData() => new EmbeddedMetaData(_sch);

    public override void Close()
    {
        _scan.Close();
        _conn.Commit();
    }
}
