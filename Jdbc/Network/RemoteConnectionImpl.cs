using DBSharp.Transactions;

namespace DBSharp.Jdbc.Network;

public class RemoteConnectionImpl : IRemoteConnection
{
    private readonly SimpleDB _db;
    private Transaction _currentTx;
    private readonly Planner.Planner _planner;

    public RemoteConnectionImpl(SimpleDB db)
    {
        _db = db;
        _currentTx = db.NewTx();
        _planner = db.GetPlanner();
    }

    public IRemoteStatement CreateStatement() =>
        new RemoteStatementImpl(this, _planner);

    public void Close() => Commit();

    internal void Commit()
    {
        _currentTx.Commit();
        _currentTx = _db.NewTx();
    }

    internal void Rollback()
    {
        _currentTx.Rollback();
        _currentTx = _db.NewTx();
    }

    internal Transaction GetTransaction() => _currentTx;
}
