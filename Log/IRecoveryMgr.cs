namespace DBSharp.Log;
using DBSharp.Buffers;

public interface IRecoveryMgr
{
    void Commit();
    void Rollback();
    void Recover();
    int SetInt(Buffer buff, int offset, int newval);
    int SetString(Buffer buff, int offset, string newval);
    int Append(string filename);
}