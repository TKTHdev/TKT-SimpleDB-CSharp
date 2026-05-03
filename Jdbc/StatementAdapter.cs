namespace DBSharp.Jdbc;

public abstract class StatementAdapter : IStatement
{
    public virtual IResultSet ExecuteQuery(string sql) =>
        throw new NotSupportedException("operation not implemented");

    public virtual int ExecuteUpdate(string sql) =>
        throw new NotSupportedException("operation not implemented");

    public virtual void Close() { }
}
