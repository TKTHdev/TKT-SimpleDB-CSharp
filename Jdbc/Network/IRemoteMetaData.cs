namespace AyeAyeDB.Jdbc.Network;

public interface IRemoteMetaData
{
    int GetColumnCount();
    string GetColumnName(int column);
    int GetColumnType(int column);
    int GetColumnDisplaySize(int column);
}
