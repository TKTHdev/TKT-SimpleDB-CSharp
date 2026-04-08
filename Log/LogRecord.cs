namespace DBSharp.Log;
using DBSharp.File;
using DBSharp.Transactions;

/// <summary>
/// Defines the contract for a write-ahead log record. Each record has an operation type,
/// an associated transaction number, and the ability to undo its effect.
/// </summary>
public interface LogRecord
{
    /// <summary>Operation type: checkpoint.</summary>
    const int CHECKPOINT = 0;
    /// <summary>Operation type: transaction start.</summary>
    const int START = 1;
    /// <summary>Operation type: transaction commit.</summary>
    const int COMMIT = 2;
    /// <summary>Operation type: transaction rollback.</summary>
    const int ROLLBACK = 3;
    /// <summary>Operation type: integer value update.</summary>
    const int SETINT = 4;
    /// <summary>Operation type: string value update.</summary>
    const int SETSTRING = 5;
    /// <summary>Operation type: non-quiescent checkpoint.</summary>
    const int NQCHECKPOINT = 6;
    /// <summary>Operation type: block append.</summary>
    const int APPEND = 7;

    /// <summary>Returns the operation type of this log record.</summary>
    int Op();

    /// <summary>Returns the transaction number stored in this log record.</summary>
    int TxNumber();

    /// <summary>
    /// Undoes the operation described by this log record against the given transaction.
    /// </summary>
    /// <param name="tx">The transaction to apply the undo to.</param>
    void Undo(Transaction tx);

    /// <summary>
    /// Factory method that deserializes a raw byte array into the appropriate <see cref="LogRecord"/> subtype.
    /// </summary>
    /// <param name="bytes">The raw bytes of the log record.</param>
    static LogRecord CreateLogRecord(byte[] bytes)
    {
        Page p = new Page(bytes);
        switch (p.GetInt(0))
        {
            case CHECKPOINT:
                return new CheckpointRecord();
            case START:
                return new StartRecord(p);
            case COMMIT:
                return new CommitRecord(p);
            case ROLLBACK:
                return new RollbackRecord(p);
            case SETINT:
                return new SetIntRecord(p);
            case SETSTRING:
                return new SetStringRecord(p);
            case NQCHECKPOINT:
                return new NQCheckpointRecord(p);
            case APPEND:
                return new AppendRecord(p);
            default:
                return null;
        }
    }
}

/// <summary>
/// A log record indicating a checkpoint. Checkpoints have no associated transaction
/// and cannot be undone.
/// </summary>
public class CheckpointRecord : LogRecord
{
    public CheckpointRecord() { }

    /// <inheritdoc/>
    public int Op()
    {
        return LogRecord.CHECKPOINT;
    }

    /// <summary>Returns -1 because checkpoints are not associated with a transaction.</summary>
    public int TxNumber()
    {
        return -1;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return "<CHECKPOINT>";
    }

    /// <summary>No-op; checkpoints cannot be undone.</summary>
    public void Undo(Transaction tx) { }

    /// <summary>
    /// Writes a CHECKPOINT record to the log and returns its LSN.
    /// </summary>
    /// <param name="lm">The log manager.</param>
    public static int WriteToLog(LogMgr lm)
    {
        byte[] rec = new byte[sizeof(int)];
        Page p = new Page(rec);
        p.SetInt(0, LogRecord.CHECKPOINT);
        return lm.Append(rec);
    }
}

/// <summary>
/// A log record indicating the start of a transaction.
/// </summary>
public class StartRecord : LogRecord
{
    private int _txnum;

    /// <summary>
    /// Deserializes a START record from the given page.
    /// </summary>
    /// <param name="p">The page containing the serialized record.</param>
    public StartRecord(Page p)
    {
        int tpos = sizeof(int);
        _txnum = p.GetInt(tpos);
    }

    /// <inheritdoc/>
    public int Op()
    {
        return LogRecord.START;
    }

    /// <inheritdoc/>
    public int TxNumber()
    {
        return _txnum;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return "<START " + _txnum + ">";
    }

    /// <summary>No-op; START records cannot be undone.</summary>
    public void Undo(Transaction tx) { }

    /// <summary>
    /// Writes a START record for the given transaction to the log.
    /// </summary>
    /// <param name="lm">The log manager.</param>
    /// <param name="txnum">The transaction number.</param>
    public static int WriteToLog(LogMgr lm, int txnum)
    {
        byte[] rec = new byte[2 * sizeof(int)];
        Page p = new Page(rec);
        p.SetInt(0, LogRecord.START);
        p.SetInt(sizeof(int), txnum);
        return lm.Append(rec);
    }
}

/// <summary>
/// A log record indicating that a transaction has committed.
/// </summary>
public class CommitRecord : LogRecord
{
    private int _txnum;

    /// <summary>
    /// Deserializes a COMMIT record from the given page.
    /// </summary>
    /// <param name="p">The page containing the serialized record.</param>
    public CommitRecord(Page p)
    {
        int tpos = sizeof(int);
        _txnum = p.GetInt(tpos);
    }

    /// <inheritdoc/>
    public int Op()
    {
        return LogRecord.COMMIT;
    }

    /// <inheritdoc/>
    public int TxNumber()
    {
        return _txnum;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return "<COMMIT " + _txnum + ">";
    }

    /// <summary>No-op; COMMIT records cannot be undone.</summary>
    public void Undo(Transaction tx) { }

    /// <summary>
    /// Writes a COMMIT record for the given transaction to the log.
    /// </summary>
    /// <param name="lm">The log manager.</param>
    /// <param name="txnum">The transaction number.</param>
    public static int WriteToLog(LogMgr lm, int txnum)
    {
        byte[] rec = new byte[2 * sizeof(int)];
        Page p = new Page(rec);
        p.SetInt(0, LogRecord.COMMIT);
        p.SetInt(sizeof(int), txnum);
        return lm.Append(rec);
    }
}

/// <summary>
/// A log record indicating that a transaction has been rolled back.
/// </summary>
public class RollbackRecord : LogRecord
{
    private int _txnum;

    /// <summary>
    /// Deserializes a ROLLBACK record from the given page.
    /// </summary>
    /// <param name="p">The page containing the serialized record.</param>
    public RollbackRecord(Page p)
    {
        int tpos = sizeof(int);
        _txnum = p.GetInt(tpos);
    }

    /// <inheritdoc/>
    public int Op()
    {
        return LogRecord.ROLLBACK;
    }

    /// <inheritdoc/>
    public int TxNumber()
    {
        return _txnum;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return "<ROLLBACK " + _txnum + ">";
    }

    /// <summary>No-op; ROLLBACK records cannot be undone.</summary>
    public void Undo(Transaction tx) { }

    /// <summary>
    /// Writes a ROLLBACK record for the given transaction to the log.
    /// </summary>
    /// <param name="lm">The log manager.</param>
    /// <param name="txnum">The transaction number.</param>
    public static int WriteToLog(LogMgr lm, int txnum)
    {
        byte[] rec = new byte[2 * sizeof(int)];
        Page p = new Page(rec);
        p.SetInt(0, LogRecord.ROLLBACK);
        p.SetInt(sizeof(int), txnum);
        return lm.Append(rec);
    }
}

/// <summary>
/// A log record that captures a string update. Stores the old value so the change can be undone.
/// Layout: [SETSTRING][txnum][filename][blocknum][offset][old value]
/// </summary>
public class SetStringRecord : LogRecord
{
    private int _txnum, _offset;
    private string _val;
    private BlockId _blk;

    /// <summary>
    /// Deserializes a SETSTRING record from the given page.
    /// </summary>
    /// <param name="p">The page containing the serialized record.</param>
    public SetStringRecord(Page p)
    {
        // example of SET STRING RECORD:
        // <SETSTRING(record type), 2(txnum), junk(filename), 44(blockid), 20(offset), hello(old value), ciao(new value)>
        int tpos = sizeof(int);
        _txnum = p.GetInt(tpos);

        int fpos = tpos + sizeof(int);
        string filename = p.GetString(fpos);

        int bpos = fpos + Page.MaxLength(filename.Length);
        int blknum = p.GetInt(bpos);
        _blk = new BlockId(filename, blknum);

        int opos = bpos + sizeof(int);
        _offset = p.GetInt(opos);

        int vpos = opos + sizeof(int);
        _val = p.GetString(vpos);
    }

    /// <inheritdoc/>
    public int Op()
    {
        return LogRecord.SETSTRING;
    }

    /// <inheritdoc/>
    public int TxNumber()
    {
        return _txnum;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return "<SETSTRING " + _txnum + " " + _blk + " " + _offset + " " + _val + ">";
    }

    /// <summary>
    /// Restores the old string value by pinning the block, writing the saved value,
    /// and unpinning. The undo itself is not logged.
    /// </summary>
    /// <param name="tx">The transaction used to perform the undo.</param>
    public void Undo(Transaction tx)
    {
        tx.Pin(_blk);
        tx.SetString(_blk, _offset, _val, false); // don't log the undo!
        tx.Unpin(_blk);
    }

    /// <summary>
    /// Writes a SETSTRING record to the log, capturing the old value for undo.
    /// </summary>
    /// <param name="lm">The log manager.</param>
    /// <param name="txnum">The transaction number.</param>
    /// <param name="blk">The block being modified.</param>
    /// <param name="offset">The byte offset within the block.</param>
    /// <param name="val">The old string value to store.</param>
    /// <returns>The LSN of the new log record.</returns>
    public static int WriteToLog(LogMgr lm, int txnum, BlockId blk, int offset, string val)
    {
        int tpos = sizeof(int);
        int fpos = tpos + sizeof(int);
        int bpos = fpos + Page.MaxLength(blk.FileName().Length);
        int opos = bpos + sizeof(int);
        int vpos = opos + sizeof(int);
        int reclen = vpos + Page.MaxLength(val.Length);
        byte[] rec = new byte[reclen];
        Page p = new Page(rec);
        p.SetInt(0, LogRecord.SETSTRING);
        p.SetInt(tpos, txnum);
        p.SetString(fpos, blk.FileName());
        p.SetInt(bpos, blk.Number());
        p.SetInt(opos, offset);
        p.SetString(vpos, val);
        return lm.Append(rec);
    }
}

/// <summary>
/// A log record that captures an integer update. Stores the old value so the change can be undone.
/// Layout: [SETINT][txnum][filename][blocknum][offset][old value]
/// </summary>
public class SetIntRecord : LogRecord
{
    private int _txnum, _offset;
    private int _val;
    private BlockId _blk;

    /// <summary>
    /// Deserializes a SETINT record from the given page.
    /// </summary>
    /// <param name="p">The page containing the serialized record.</param>
    public SetIntRecord(Page p)
    {
        int tpos = sizeof(int);
        _txnum = p.GetInt(tpos);

        int fpos = tpos + sizeof(int);
        string filename = p.GetString(fpos);

        int bpos = fpos + Page.MaxLength(filename.Length);
        int blknum = p.GetInt(bpos);
        _blk = new BlockId(filename, blknum);

        int opos = bpos + sizeof(int);
        _offset = p.GetInt(opos);

        int vpos = opos + sizeof(int);
        _val = p.GetInt(vpos);
    }

    /// <inheritdoc/>
    public int Op()
    {
        return LogRecord.SETINT;
    }

    /// <inheritdoc/>
    public int TxNumber()
    {
        return _txnum;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return "<SETINT " + _txnum + " " + _blk + " " + _offset + " " + _val + ">";
    }

    /// <summary>
    /// Restores the old integer value by pinning the block, writing the saved value,
    /// and unpinning. The undo itself is not logged.
    /// </summary>
    /// <param name="tx">The transaction used to perform the undo.</param>
    public void Undo(Transaction tx)
    {
        tx.Pin(_blk);
        tx.SetInt(_blk, _offset, _val, false);
        tx.Unpin(_blk);
    }

    /// <summary>
    /// Writes a SETINT record to the log, capturing the old value for undo.
    /// </summary>
    /// <param name="lm">The log manager.</param>
    /// <param name="txnum">The transaction number.</param>
    /// <param name="blk">The block being modified.</param>
    /// <param name="offset">The byte offset within the block.</param>
    /// <param name="val">The old integer value to store.</param>
    /// <returns>The LSN of the new log record.</returns>
    public static int WriteToLog(LogMgr lm, int txnum, BlockId blk, int offset, int val)
    {
        int tpos = sizeof(int);
        int fpos = tpos + sizeof(int);
        int bpos = fpos + Page.MaxLength(blk.FileName().Length);
        int opos = bpos + sizeof(int);
        int vpos = opos + sizeof(int);
        int reclen = vpos + sizeof(int);
        byte[] rec = new byte[reclen];
        Page p = new Page(rec);
        p.SetInt(0, LogRecord.SETINT);
        p.SetInt(tpos, txnum);
        p.SetString(fpos, blk.FileName());
        p.SetInt(bpos, blk.Number());
        p.SetInt(opos, offset);
        p.SetInt(vpos, val);
        return lm.Append(rec);
    }
}

/// <summary>
/// A log record indicating a non-quiescent checkpoint. Stores the list of active
/// transaction numbers at the time of the checkpoint, so that recovery knows which
/// transactions may still have been in progress.
/// Layout: [NQCHECKPOINT][txn count][txnum1][txnum2]...
/// </summary>
public class NQCheckpointRecord : LogRecord
{
    private List<int> _txnums;

    /// <summary>
    /// Creates a non-quiescent checkpoint record with the given active transaction list.
    /// </summary>
    /// <param name="txnums">The list of active transaction numbers.</param>
    public NQCheckpointRecord(List<int> txnums)
    {
        _txnums = txnums;
    }

    /// <summary>
    /// Deserializes a NQCHECKPOINT record from the given page.
    /// </summary>
    /// <param name="p">The page containing the serialized record.</param>
    public NQCheckpointRecord(Page p)
    {
        int tpos = sizeof(int);
        int count = p.GetInt(tpos);
        _txnums = new List<int>();
        int pos = tpos + sizeof(int);
        for (int i = 0; i < count; i++)
        {
            _txnums.Add(p.GetInt(pos));
            pos += sizeof(int);
        }
    }

    /// <inheritdoc/>
    public int Op()
    {
        return LogRecord.NQCHECKPOINT;
    }

    /// <summary>Returns -1 because checkpoints are not associated with a single transaction.</summary>
    public int TxNumber()
    {
        return -1;
    }

    /// <summary>
    /// Returns the list of transaction numbers that were active at checkpoint time.
    /// </summary>
    public IReadOnlyList<int> ActiveTxns => _txnums.AsReadOnly();

    /// <inheritdoc/>
    public override string ToString()
    {
        return "<NQCHECKPOINT " + string.Join(",", _txnums) + ">";
    }

    /// <summary>No-op; checkpoints cannot be undone.</summary>
    public void Undo(Transaction tx) { }

    /// <summary>
    /// Writes a NQCHECKPOINT record to the log with the list of active transactions.
    /// </summary>
    /// <param name="lm">The log manager.</param>
    /// <param name="txnums">The active transaction numbers at checkpoint time.</param>
    public static int WriteToLog(LogMgr lm, List<int> txnums)
    {
        // Layout: [NQCHECKPOINT(int)][count(int)][txnum1(int)][txnum2(int)]...
        int reclen = sizeof(int) + sizeof(int) + txnums.Count * sizeof(int);
        byte[] rec = new byte[reclen];
        Page p = new Page(rec);
        p.SetInt(0, LogRecord.NQCHECKPOINT);
        p.SetInt(sizeof(int), txnums.Count);
        int pos = 2 * sizeof(int);
        foreach (int txnum in txnums)
        {
            p.SetInt(pos, txnum);
            pos += sizeof(int);
        }
        return lm.Append(rec);
    }
}

/// <summary>
/// A log record that captures a block append operation so it can be undone during recovery.
/// Undo truncates the last block from the file.
/// Layout: [APPEND][txnum][filename]
/// </summary>
public class AppendRecord : LogRecord
{
    private int _txnum;
    private string _filename;

    /// <summary>
    /// Deserializes an APPEND record from the given page.
    /// </summary>
    /// <param name="p">The page containing the serialized record.</param>
    public AppendRecord(Page p)
    {
        int tpos = sizeof(int);
        _txnum = p.GetInt(tpos);

        int fpos = tpos + sizeof(int);
        _filename = p.GetString(fpos);
    }

    /// <inheritdoc/>
    public int Op() => LogRecord.APPEND;

    /// <inheritdoc/>
    public int TxNumber() => _txnum;

    /// <inheritdoc/>
    public override string ToString()
    {
        return "<APPEND " + _txnum + " " + _filename + ">";
    }

    /// <summary>
    /// Undoes the append by truncating the last block from the file.
    /// </summary>
    /// <param name="tx">The transaction used to perform the undo.</param>
    public void Undo(Transaction tx)
    {
        tx.Truncate(_filename);
    }

    /// <summary>
    /// Writes an APPEND record to the log.
    /// </summary>
    /// <param name="lm">The log manager.</param>
    /// <param name="txnum">The transaction number.</param>
    /// <param name="filename">The name of the file that was appended to.</param>
    /// <returns>The LSN of the new log record.</returns>
    public static int WriteToLog(LogMgr lm, int txnum, string filename)
    {
        int tpos = sizeof(int);
        int fpos = tpos + sizeof(int);
        int reclen = fpos + Page.MaxLength(filename.Length);
        byte[] rec = new byte[reclen];
        Page p = new Page(rec);
        p.SetInt(0, LogRecord.APPEND);
        p.SetInt(tpos, txnum);
        p.SetString(fpos, filename);
        return lm.Append(rec);
    }
}

