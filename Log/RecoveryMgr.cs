using DBSharp.Transactions;
using DBSharp.Buffers;
using DBSharp.File;
using Buffer = DBSharp.Buffers.Buffer;

namespace DBSharp.Log;

public class RecoveryMgr
{
    private LogMgr _lm;
    private BufferMgr _bm;
    private Transaction _tx;
    private int _txnum;

    public RecoveryMgr(Transaction tx, int txnum, LogMgr lm, BufferMgr bm)
    {
        _tx = tx;
        _txnum = txnum;
        _lm = lm;
        _bm = bm;
        StartRecord.WriteToLog(lm, txnum);
    }

    public void Commit()
    {
        _bm.FlushAll(_txnum);
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
        DoRecover();
        _bm.FlushAll(_txnum);
        int lsn = CheckpointRecord.WriteToLog(_lm);
        _lm.Flush(lsn);
    }

    public int SetInt(Buffer buff, int offset, int newval)
    {
        int oldval = buff.Contents().GetInt(offset);
        BlockId blk = buff.Block();
        return SetIntRecord.WriteToLog(_lm, _txnum, blk, offset, oldval);
    }

    public int SetString(Buffer buff, int offset, String newval)
    {
        string oldval = buff.Contents().GetString(offset);
        BlockId blk = buff.Block();
        return SetStringRecord.WriteToLog(_lm, _txnum, blk, offset, newval);
    }

    private void DoRollback()
    {
        foreach (byte[] bytes in _lm.GetEnumerator())
        {
            LogRecord rec = LogRecord.CreateLogRecord(bytes);
            if (rec.TxNumber() == _txnum)
            {
                if (rec.Op() == LogRecord.START)
                    return;
                rec.Undo(_tx);
            }
        }
    }

    private void DoRecover()
    {
        List<int> finishedTxs = new List<int>();
        foreach (byte[] bytes in _lm.GetEnumerator())
        {
            LogRecord rec = LogRecord.CreateLogRecord(bytes);
            if (rec.Op() == LogRecord.CHECKPOINT)
                return;
            if (rec.Op() == LogRecord.COMMIT || rec.Op() == LogRecord.ROLLBACK)
                finishedTxs.Add(rec.TxNumber());
            else if (!finishedTxs.Contains(rec.TxNumber()))
                rec.Undo(_tx);
        }
    }
}
