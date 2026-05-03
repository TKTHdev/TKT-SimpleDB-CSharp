using DBSharp.Transactions;

namespace DBSharp.Jdbc.Embedded;

public class EmbeddedConnection : ConnectionAdapter
{
    private readonly SimpleDB _db;
    private Transaction _currentTx;
    private readonly Planner.Planner _planner;

    public EmbeddedConnection(SimpleDB db)
    {
        _db = db;
        _currentTx = db.NewTx();
        _planner = db.GetPlanner();
    }

    public override IStatement CreateStatement() =>
        new EmbeddedStatement(this, _planner);

    public override void Close() => Commit();

    public override void Commit()
    {
        _currentTx.Commit();
        _currentTx = _db.NewTx();
    }

    public override void Rollback()
    {
        _currentTx.Rollback();
        _currentTx = _db.NewTx();
    }

    internal Transaction GetTransaction() => _currentTx;
}
