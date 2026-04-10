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
        // collect all records that may need redo
        List<byte[]> recordsToRedo = new List<byte[]>();
        // when we hit a NQCHECKPOINT, this holds the txns
        // that were active at checkpoint time and have not yet finished.
        HashSet<int> unresolvedTxns = null;

        // Phase 1: Backward scan (undo pass)
        // undo uncommitted transactions and collect records for redo
        foreach (byte[] bytes in _lm.GetBackwardEnumerator())
        {
            LogRecord rec = LogRecord.CreateLogRecord(bytes);

            if (rec.Op() == LogRecord.CHECKPOINT)
                break;

            if (rec.Op() == LogRecord.NQCHECKPOINT)
            {
                var nqRec = (NQCheckpointRecord)rec;
                unresolvedTxns = new HashSet<int>();
                foreach (int txnum in nqRec.ActiveTxns)
                {
                    if (!committedTxns.Contains(txnum) && !rolledBackTxns.Contains(txnum))
                        unresolvedTxns.Add(txnum);
                }
                // if all active txns at checkpoint time have already finished,
                // we can stop scanning
                if (unresolvedTxns.Count == 0)
                    break;
                continue;
            }

            if (rec.Op() == LogRecord.COMMIT)
            {
                committedTxns.Add(rec.TxNumber());
                unresolvedTxns?.Remove(rec.TxNumber());
            }
            else if (rec.Op() == LogRecord.ROLLBACK)
            {
                rolledBackTxns.Add(rec.TxNumber());
                unresolvedTxns?.Remove(rec.TxNumber());
            }
            else if (unresolvedTxns != null)
            {
                // after NQCHECKPOINT: only process records for unresolved txns
                if (unresolvedTxns.Contains(rec.TxNumber()))
                {
                    if (rec.Op() == LogRecord.START)
                    {
                        unresolvedTxns.Remove(rec.TxNumber());
                        if (unresolvedTxns.Count == 0)
                            break;
                    }
                    else
                    {
                        recordsToRedo.Add(bytes);
                        if (committedTxns.Contains(rec.TxNumber()) == false)
                            rec.Undo(_tx);
                    }
                }
            }
            else
            {
                // before NQCHECKPOINT (or no checkpoint at all)
                recordsToRedo.Add(bytes);
                if (committedTxns.Contains(rec.TxNumber()) == false && rolledBackTxns.Contains(rec.TxNumber()) == false)
                    rec.Undo(_tx);
            }
        }

        // Phase 2: Forward scan (redo pass)
        // redo committed transactions whose data may not be on disk (no-force policy)
        recordsToRedo.Reverse();
        foreach (byte[] bytes in recordsToRedo)
        {
            LogRecord rec = LogRecord.CreateLogRecord(bytes);
            if (committedTxns.Contains(rec.TxNumber()))
                rec.Redo(_tx);
        }
    }


}