using DBSharp.Transactions;
using DBSharp.Buffers;
using DBSharp.File;
using Buffer = DBSharp.Buffers.Buffer;

namespace DBSharp.Log;

public class RecoveryMgr
{
    private LogMgr lm;
    private BufferMgr bm;
    private Transaction tx;
    private int txnum;
    
    public RecoveryMgr(Transaction tx, int txnum, LogMgr lm, BufferMgr bm)
    {
        this.tx  = tx;
        this.txnum = txnum;
        this.lm = lm;
        this.bm = bm;
        StartRecord.writeToLog(lm, txnum);
    }

    public void commit()
    {
        bm.FlushAll(txnum);
        int lsn = CommitRecord.writeToLog(lm, txnum);
        lm.Flush(lsn);
    }

    public void rollback()
    {
        doRollback();
        bm.FlushAll(txnum);
        int lsn = RollbackRecord.writeToLog(lm, txnum);
        lm.Flush(lsn);
    }

    public void recover()
    {
        doRecover();
        bm.FlushAll(txnum);
        int lsn = CheckpointRecord.writeToLog(lm);
        lm.Flush(lsn);
    }

    public int setInt(Buffer buff, int offset, int newval)
    {
        int oldval = buff.Contents().GetInt(offset);
        BlockId blk = buff.Block();
        return SetIntRecord.writeToLog(lm, txnum, blk, offset, oldval);
    }

    public int setString(Buffer buff, int offset, String newval)
    {
        string oldval = buff.Contents().GetString(offset);
        BlockId blk = buff.Block();
        return SetStringRecord.writeToLog(lm, txnum, blk, offset, newval);
    }
    private void  doRollback()
    {}
    private void  doRecover()
    {}
}
