using DBSharp.Transactions;
using DBSharp.Buffers;
using DBSharp.File;
using Buffer = DBSharp.Buffers.Buffer;

namespace DBSharp.Log;

/// <summary>
/// Handles transaction recovery using the write-ahead log. Provides commit, rollback,
/// and crash-recovery operations by reading log records and undoing uncommitted changes.
/// </summary>
public class RecoveryMgr
{
    private LogMgr _lm;
    private IBufferMgr _bm;
    private Transaction _tx;
    private int _txnum;

    /// <summary>
    /// Creates a recovery manager for the given transaction and writes a START record to the log.
    /// </summary>
    /// <param name="tx">The owning transaction.</param>
    /// <param name="txnum">The transaction number.</param>
    /// <param name="lm">The log manager.</param>
    /// <param name="bm">The buffer manager.</param>
    public RecoveryMgr(Transaction tx, int txnum, LogMgr lm, IBufferMgr bm)
    {
        _tx = tx;
        _txnum = txnum;
        _lm = lm;
        _bm = bm;
        StartRecord.WriteToLog(lm, txnum);
    }

    /// <summary>
    /// Commits the transaction by flushing all modified buffers and writing a COMMIT log record.
    /// </summary>
    public void Commit()
    {
        _bm.FlushAll(_txnum);
        int lsn = CommitRecord.WriteToLog(_lm, _txnum);
        _lm.Flush(lsn);
    }

    /// <summary>
    /// Rolls back the transaction by undoing all its changes, then writing a ROLLBACK log record.
    /// </summary>
    public void Rollback()
    {
        DoRollback();
        _bm.FlushAll(_txnum);
        int lsn = RollbackRecord.WriteToLog(_lm, _txnum);
        _lm.Flush(lsn);
    }

    /// <summary>
    /// Performs crash recovery by undoing all uncommitted transactions found in the log,
    /// then writing a CHECKPOINT record.
    /// </summary>
    public void Recover()
    {
        DoRecover();
        _bm.FlushAll(_txnum);
        int lsn = CheckpointRecord.WriteToLog(_lm);
        _lm.Flush(lsn);
    }

    /// <summary>
    /// Logs the old integer value before an update so it can be undone during rollback.
    /// </summary>
    /// <param name="buff">The buffer containing the block being modified.</param>
    /// <param name="offset">The byte offset of the value within the block.</param>
    /// <param name="newval">The new value (unused for logging; the old value is read from the buffer).</param>
    /// <returns>The LSN of the log record.</returns>
    public int SetInt(Buffer buff, int offset, int newval)
    {
        int oldval = buff.Contents().GetInt(offset);
        BlockId blk = buff.Block();
        return SetIntRecord.WriteToLog(_lm, _txnum, blk, offset, oldval);
    }

    /// <summary>
    /// Logs the old string value before an update so it can be undone during rollback.
    /// </summary>
    /// <param name="buff">The buffer containing the block being modified.</param>
    /// <param name="offset">The byte offset of the value within the block.</param>
    /// <param name="newval">The new value (unused for logging; the old value is read from the buffer).</param>
    /// <returns>The LSN of the log record.</returns>
    public int SetString(Buffer buff, int offset, String newval)
    {
        string oldval = buff.Contents().GetString(offset);
        BlockId blk = buff.Block();
        return SetStringRecord.WriteToLog(_lm, _txnum, blk, offset, oldval);
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
