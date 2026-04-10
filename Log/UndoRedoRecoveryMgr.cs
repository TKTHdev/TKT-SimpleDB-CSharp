using DBSharp.Buffers;
using Buffer = DBSharp.Buffers.Buffer;
using DBSharp.Transactions;
using DBSharp.File;
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
        DoRollback();
        _bm.FlushAll(_txnum);
        int lsn = RollbackRecord.WriteToLog(_lm, _txnum);
        _lm.Flush(lsn);
    }

    public void Recover()
    {
        throw new NotImplementedException();
    }

    public int SetInt(Buffer buff, int offset, int newval)
    {
        int oldval = buff.Contents().GetInt(offset);
        BlockId blk = buff.Block();
        return SetIntRecord.WriteToLog(_lm, _txnum, blk, offset, oldval, newval);
    }

    public int SetString(Buffer buff, int offset, string newval)
    {
        string oldval = buff.Contents().GetString(offset);
        BlockId blk = buff.Block();
        return SetStringRecord.WriteToLog(_lm, _txnum, blk, offset, oldval, newval);   
    }

    public int Append(string filename)
    {
        return AppendRecord.WriteToLog(_lm, _txnum, filename);
    }

    private void DoRollback()
    {
        // iterate in reverse order
        foreach (byte[] bytes in _lm.GetEnumerator())
        {
            LogRecord rec = LogRecord.CreateLogRecord(bytes);
            // if the record is by the corresponding transaction
            if (rec.TxNumber() == _txnum)
            {
                // undo until it reaches START record
                if (rec.Op() == LogRecord.START)
                    return;
                rec.Undo(_tx);
            }
        }
    }
    

}