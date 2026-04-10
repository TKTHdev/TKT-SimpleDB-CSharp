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
        // no need to flush all the buffers
        // since it will be redoed anyways
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
        foreach (byte[] bytes in _lm.GetBackwardEnumerator())
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

    private void DoRecover()
    {
        List<int> committedTxns = new List<int>();
        List<int> rolledBackTxns = new List<int>();
        // collect all records between the latest checkpoint and the end of the log
        List<byte[]> recordsSinceCheckpoint = new List<byte[]>();

        // Phase 1: Backward scan (undo pass)
        // undo uncommitted transactions and collect records for redo
        foreach (byte[] bytes in _lm.GetBackwardEnumerator())
        {
            LogRecord rec = LogRecord.CreateLogRecord(bytes);
            if (rec.Op() == LogRecord.CHECKPOINT)
                break;

            recordsSinceCheckpoint.Add(bytes);

            if (rec.Op() == LogRecord.COMMIT)
                committedTxns.Add(rec.TxNumber());

            if (rec.Op() == LogRecord.ROLLBACK)
                rolledBackTxns.Add(rec.TxNumber());

            // if the transaction is not in the committed/rolled-back list
            // then it was in-flight at crash time, so undo it
            // (no-op for START/COMMIT/ROLLBACK/CHECKPOINT records)
            if (committedTxns.Contains(rec.TxNumber()) == false && rolledBackTxns.Contains(rec.TxNumber()) == false)
                rec.Undo(_tx);
        }

        // Phase 2: Forward scan (redo pass)
        // redo committed transactions whose data may not be on disk (no-force policy)
        recordsSinceCheckpoint.Reverse();
        foreach (byte[] bytes in recordsSinceCheckpoint)
        {
            LogRecord rec = LogRecord.CreateLogRecord(bytes);
            if (committedTxns.Contains(rec.TxNumber()))
                rec.Redo(_tx);
        }
    }


}