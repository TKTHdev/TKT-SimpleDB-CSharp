namespace AyeAyeDB.Jdbc.Network;

public interface IRemoteConnection
{
    IRemoteStatement CreateStatement();
    void Close();
}
