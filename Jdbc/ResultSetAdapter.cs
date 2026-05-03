namespace AyeAyeDB.Jdbc;

public abstract class ResultSetAdapter : IResultSet
{
    public virtual bool Next() =>
        throw new NotSupportedException("operation not implemented");

    public virtual int GetInt(string fieldname) =>
        throw new NotSupportedException("operation not implemented");

    public virtual string GetString(string fieldname) =>
        throw new NotSupportedException("operation not implemented");

    public virtual IResultSetMetaData GetMetaData() =>
        throw new NotSupportedException("operation not implemented");

    public virtual void Close() { }
}
