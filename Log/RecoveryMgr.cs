using DBSharp.Transactions;
using DBSharp.Buffers;
using Buffer = DBSharp.Buffers.Buffer;

namespace DBSharp.Log;

public class RecoveryMgr
{
    public RecoveryMgr(Transaction tx, int txnum, LogMgr lm, BufferMgr bm)
    {
    }

    public void commit()
    {
    }

    public void rollback()
    {
    }

    public void recover()
    {
    }

    public int setInt(Buffer buff, int offset, int newval)
    {
        return -1;
    }

    public int setString(Buffer buff, int offset, String newval)
    {
        return -1;
    }

}
