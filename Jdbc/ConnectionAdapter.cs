namespace DBSharp.Jdbc;

public abstract class ConnectionAdapter : IConnection
{
    public virtual IStatement CreateStatement() =>
        throw new NotSupportedException("operation not implemented");

    public virtual void Close() =>
        throw new NotSupportedException("operation not implemented");

    public virtual void Commit() =>
        throw new NotSupportedException("operation not implemented");

    public virtual void Rollback() =>
        throw new NotSupportedException("operation not implemented");
}
