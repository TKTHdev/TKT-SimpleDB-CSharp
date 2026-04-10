using DBSharp.Buffers;
using Buffer = DBSharp.Buffers.Buffer;
using DBSharp.Transactions;
using DBSharp.File;
namespace DBSharp.Log;

/// <summary>
/// Undo/Redo recovery manager. Unlike <see cref="UndoOnlyRecoveryMgr"/>, this manager
/// uses a no-force policy: committed data does not need to be flushed to disk at commit time.
/// Instead, committed changes are replayed during recovery (redo pass), while uncommitted
/// changes are reverted (undo pass). Log records store both old and new values.
/// </summary>
public class UndoRedoRecoveryMgr : IRecoveryMgr
{
    private LogMgr _lm;
    private IBufferMgr _bm;
    private Transaction _tx;
    private int _txnum;

    /// <summary>
    /// Initializes the recovery manager for the given transaction and writes a START record to the log.
    /// </summary>
    /// <param name="tx">The owning transaction.</param>
    /// <param name="txnum">The transaction number.</param>
    /// <param name="lm">The log manager for WAL operations.</param>
    /// <param name="bm">The buffer manager for buffer pool access.</param>
    public  UndoRedoRecoveryMgr(Transaction tx, int txnum, LogMgr lm, IBufferMgr bm)
    {
       _tx = tx;
       _txnum = txnum;
       _lm = lm;
       _bm = bm;
       StartRecord.WriteToLog(lm, txnum);
    }

    /// <summary>
    /// Commits the transaction by writing a COMMIT record and flushing the log.
    /// Unlike undo-only recovery, modified buffers are not forced to disk.
    /// </summary>
    public void Commit()
    {
        // unlike undo-only recovery,
        // no need to flush all the modified buffers before commit
        int lsn = CommitRecord.WriteToLog(_lm, _txnum);
        _lm.Flush(lsn);
    }

    /// <summary>
    /// Rolls back the transaction by undoing all its changes, then writing a ROLLBACK record.
    /// Modified buffers are not forced to disk since redo will replay them if needed.
    /// </summary>
    public void Rollback()
    {
        DoRollback();
        // no need to flush all the buffers
        // since it will be redoed anyways
        int lsn = RollbackRecord.WriteToLog(_lm, _txnum);
        _lm.Flush(lsn);
    }

    /// <summary>
    /// Performs crash recovery: runs an undo pass to revert uncommitted transactions
    /// and a redo pass to replay committed transactions, then flushes all buffers
    /// and writes a CHECKPOINT record.
    /// </summary>
    public void Recover()
    {
        DoRecover();
        _bm.FlushAll(_txnum);
        int lsn = CheckpointRecord.WriteToLog(_lm);
        _lm.Flush(lsn);
    }

    /// <summary>
    /// Logs a SETINT record capturing both the old and new integer values
    /// for the specified buffer and offset.
    /// </summary>
    /// <param name="buff">The buffer being modified.</param>
    /// <param name="offset">The byte offset within the block.</param>
    /// <param name="newval">The new integer value.</param>
    /// <returns>The LSN of the new log record.</returns>
    public int SetInt(Buffer buff, int offset, int newval)
    {
        int oldval = buff.Contents().GetInt(offset);
        BlockId blk = buff.Block();
        return SetIntRecord.WriteToLog(_lm, _txnum, blk, offset, oldval, newval);
    }

    /// <summary>
    /// Logs a SETSTRING record capturing both the old and new string values
    /// for the specified buffer and offset.
    /// </summary>
    /// <param name="buff">The buffer being modified.</param>
    /// <param name="offset">The byte offset within the block.</param>
    /// <param name="newval">The new string value.</param>
    /// <returns>The LSN of the new log record.</returns>
    public int SetString(Buffer buff, int offset, string newval)
    {
        string oldval = buff.Contents().GetString(offset);
        BlockId blk = buff.Block();
        return SetStringRecord.WriteToLog(_lm, _txnum, blk, offset, oldval, newval);
    }

    /// <summary>
    /// Logs an APPEND record for the specified file.
    /// </summary>
    /// <param name="filename">The name of the file being extended.</param>
    /// <returns>The LSN of the new log record.</returns>
    public int Append(string filename)
    {
        return AppendRecord.WriteToLog(_lm, _txnum, filename);
    }

    /// <summary>
    /// Performs a backward scan of the log, undoing all changes made by this transaction
    /// until the START record is reached.
    /// </summary>
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

    /// <summary>
    /// Performs two-pass crash recovery:
    /// <list type="number">
    ///   <item><description>
    ///     Backward scan (undo pass): scans the log from end to beginning, undoing changes
    ///     from uncommitted and non-rolled-back transactions while collecting records for redo.
    ///     Stops at a CHECKPOINT or when all active transactions at a NQCHECKPOINT are resolved.
    ///   </description></item>
    ///   <item><description>
    ///     Forward scan (redo pass): replays collected records for committed transactions
    ///     to restore data that may not have been flushed to disk (no-force policy).
    ///   </description></item>
    /// </list>
    /// </summary>
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