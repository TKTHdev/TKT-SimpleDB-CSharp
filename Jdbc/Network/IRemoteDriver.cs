namespace DBSharp.Jdbc.Network;

public interface IRemoteDriver
{
    IRemoteConnection Connect();
}
