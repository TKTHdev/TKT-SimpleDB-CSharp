using AyeAyeDB.Planner;
using AyeAyeDB.Record;
using AyeAyeDB.Scan;

namespace AyeAyeDB.Jdbc.Network;

public class RemoteResultSetImpl : IRemoteResultSet
{
    private readonly IScan _scan;
    private readonly Schema _sch;
    private readonly RemoteConnectionImpl _conn;

    public RemoteResultSetImpl(IPlan plan, RemoteConnectionImpl conn)
    {
        _scan = plan.Open();
        _sch = plan.Schema();
        _conn = conn;
    }

    public bool Next()
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

    public int GetInt(string fieldname)
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

    public string GetString(string fieldname)
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

    public IRemoteMetaData GetMetaData() => new RemoteMetaDataImpl(_sch);

    public void Close()
    {
        _scan.Close();
        _conn.Commit();
    }
}
