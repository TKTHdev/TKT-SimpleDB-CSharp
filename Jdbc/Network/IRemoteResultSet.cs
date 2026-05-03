namespace DBSharp.Jdbc.Network;

public interface IRemoteResultSet
{
    bool Next();
    int GetInt(string fieldname);
    string GetString(string fieldname);
    IRemoteMetaData GetMetaData();
    void Close();
}
