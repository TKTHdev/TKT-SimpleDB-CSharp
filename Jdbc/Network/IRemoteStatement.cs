namespace DBSharp.Jdbc.Network;

public interface IRemoteStatement
{
    IRemoteResultSet ExecuteQuery(string sql);
    int ExecuteUpdate(string sql);
}
