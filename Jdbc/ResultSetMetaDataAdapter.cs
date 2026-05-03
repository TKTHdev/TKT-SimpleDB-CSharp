namespace DBSharp.Jdbc;

public abstract class ResultSetMetaDataAdapter : IResultSetMetaData
{
    public virtual int GetColumnCount() =>
        throw new NotSupportedException("operation not implemented");

    public virtual string GetColumnName(int column) =>
        throw new NotSupportedException("operation not implemented");

    public virtual int GetColumnType(int column) =>
        throw new NotSupportedException("operation not implemented");

    public virtual int GetColumnDisplaySize(int column) =>
        throw new NotSupportedException("operation not implemented");
}
