using Buffer = DBSharp.Buffers.Buffer;

namespace DBSharp.Log;

public class UndoRedoRecoveryMgr : IRecoveryMgr
{
    public void Commit()
    {
        throw new NotImplementedException();
    }

    public void Rollback()
    {
        throw new NotImplementedException();
    }

    public void Recover()
    {
        throw new NotImplementedException();
    }

    public int SetInt(Buffer buff, int offset, int newval)
    {
        throw new NotImplementedException();
    }

    public int SetString(Buffer buff, int offset, string newval)
    {
        throw new NotImplementedException();
    }

    public int Append(string filename)
    {
        throw new NotImplementedException();
    }
}