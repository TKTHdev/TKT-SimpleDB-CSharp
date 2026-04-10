using DBSharp.Buffers;
using Buffer = DBSharp.Buffers.Buffer;
using DBSharp.Transactions;
namespace DBSharp.Log;

public class UndoRedoRecoveryMgr : IRecoveryMgr
{
    private LogMgr _lm;
    private IBufferMgr _bm;
    private Transaction _tx;
    private int _txnum;

    public  UndoRedoRecoveryMgr(Transaction tx, int txnum, LogMgr lm, IBufferMgr bm)
    {
       _tx = tx;
       _txnum = txnum;
       _lm = lm;
       _bm = bm;
       StartRecord.WriteToLog(lm, txnum);
    }
    public void Commit()
    {
        // unlike undo-only recovery,
        // no need to flush all the modified buffers before commit
        int lsn = CommitRecord.WriteToLog(_lm, _txnum);
        _lm.Flush(lsn);
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