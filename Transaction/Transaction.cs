using System.Collections.Concurrent;
using DBSharp.Buffers;
using DBSharp.File;
using DBSharp.Concurrency;
using DBSharp.Log;
namespace DBSharp.Transactions;
using Buffer = DBSharp.Buffers.Buffer;

/// <summary>
/// Represents a database transaction. Coordinates concurrency control, buffer management,
/// and recovery to provide ACID guarantees. Each transaction is assigned a unique
/// auto-incrementing transaction number.
/// </summary>
public class Transaction
{
    private static int _nextTxNum = 0;
    private static int END_OF_FILE = -1;
    private IRecoveryMgr _recoveryMgr;
    private ConcurrencyMgr _concurMgr;
    private IBufferMgr _bm;
    private FileMgr _fm;
    private int _txnum;
    private BufferList _myBuffers;
    private static readonly object _locker = new object();
    private static readonly ConcurrentDictionary<int, bool> _runningTxns = new();
    private static readonly ManualResetEventSlim _checkpointGate = new(initialState: true); // signaled = not checkpointing

    /// <summary>
    /// Returns a snapshot of all currently running transaction numbers.
    /// </summary>
    public static IReadOnlyCollection<int> RunningTxns => _runningTxns.Keys.ToList().AsReadOnly();

    /// <summary>
    /// Signals that a checkpoint has started. New transactions will block until EndCheckpoint is called.
    /// </summary>
    public static void StartCheckpoint() => _checkpointGate.Reset();

    /// <summary>
    /// Signals that the checkpoint has finished. Blocked transactions may now proceed.
    /// </summary>
    public static void EndCheckpoint() => _checkpointGate.Set();

    /// <summary>
    /// Creates a new transaction with a unique transaction number, initializing
    /// its recovery manager, concurrency manager, and buffer list.
    /// </summary>
    /// <param name="fm">The file manager for disk I/O.</param>
    /// <param name="lm">The log manager for WAL operations.</param>
    /// <param name="bm">The buffer manager for buffer pool access.</param>
    public Transaction(FileMgr fm, LogMgr lm, IBufferMgr bm)
        : this(fm, lm, bm, useUndoRedo: false) { }

    public Transaction(FileMgr fm, LogMgr lm, IBufferMgr bm, bool useUndoRedo)
    {
        // wait when checkpointing is running
        _checkpointGate.Wait();
        _fm = fm;
        _bm = bm;
        _txnum = NextTxNumber();
        _runningTxns.TryAdd(_txnum, true);
        _recoveryMgr = useUndoRedo
            ? new UndoRedoRecoveryMgr(this, _txnum, lm, bm)
            : new UndoOnlyRecoveryMgr(this, _txnum, lm, bm);
        _concurMgr = new ConcurrencyMgr(_txnum);
        _myBuffers = new BufferList(bm);
    }

    /// <summary>
    /// Commits the transaction: flushes log and buffers, releases all locks, and unpins all buffers.
    /// </summary>
    public void Commit()
    {
        _recoveryMgr.Commit();
        _concurMgr.Release();
        _myBuffers.UnpinAll();
        _runningTxns.TryRemove(_txnum, out _);
        Console.WriteLine("transaction" + _txnum + " committed");
    }

    /// <summary>
    /// Rolls back the transaction: undoes all changes, releases locks, and unpins all buffers.
    /// </summary>
    public void Rollback()
    {
        _recoveryMgr.Rollback();
        _concurMgr.Release();
        _myBuffers.UnpinAll();
        _runningTxns.TryRemove(_txnum, out _);
        Console.WriteLine("transaction" + _txnum + " rolled back");
    }

    /// <summary>
    /// Performs crash recovery by flushing all buffers and undoing uncommitted transactions.
    /// </summary>
    public void Recover()
    {
        _bm.FlushAll(_txnum);
        _recoveryMgr.Recover();
    }

    /// <summary>
    /// Pins the specified block, adding it to this transaction's buffer list.
    /// </summary>
    /// <param name="blk">The block to pin.</param>
    public void Pin(BlockId blk)
    {
        _myBuffers.Pin(blk);
    }

    /// <summary>
    /// Unpins the specified block from this transaction's buffer list.
    /// </summary>
    /// <param name="blk">The block to unpin.</param>
    public void Unpin(BlockId blk)
    {
        _myBuffers.Unpin(blk);
    }

    /// <summary>
    /// Reads an integer from the specified block and offset after acquiring a shared lock.
    /// </summary>
    /// <param name="blk">The block to read from.</param>
    /// <param name="offset">The byte offset within the block.</param>
    public int GetInt(BlockId blk, int offset)
    {
        _concurMgr.SLock(blk);
        Buffer buff = _myBuffers.GetBuffer(blk);
        return buff.Contents().GetInt(offset);
    }

    /// <summary>
    /// Reads a string from the specified block and offset after acquiring a shared lock.
    /// </summary>
    /// <param name="blk">The block to read from.</param>
    /// <param name="offset">The byte offset within the block.</param>
    public string GetString(BlockId blk, int offset)
    {
        _concurMgr.SLock(blk);
        Buffer buff = _myBuffers.GetBuffer(blk);
        return buff.Contents().GetString(offset);
    }

    /// <summary>
    /// Writes an integer to the specified block and offset after acquiring an exclusive lock.
    /// Optionally logs the old value for undo.
    /// </summary>
    /// <param name="blk">The block to write to.</param>
    /// <param name="offset">The byte offset within the block.</param>
    /// <param name="val">The integer value to write.</param>
    /// <param name="okToLog">Whether to write a log record for this change.</param>
    public void SetInt(BlockId blk, int offset, int val, bool okToLog)
    {
        _concurMgr.XLock(blk);
        Buffer buff = _myBuffers.GetBuffer(blk);
        int lsn = -1;
        if (okToLog)
            lsn = _recoveryMgr.SetInt(buff, offset, val);
        Page p = buff.Contents();
        p.SetInt(offset, val);
        buff.SetModified(_txnum, lsn);
    }

    /// <summary>
    /// Writes a string to the specified block and offset after acquiring an exclusive lock.
    /// Optionally logs the old value for undo.
    /// </summary>
    /// <param name="blk">The block to write to.</param>
    /// <param name="offset">The byte offset within the block.</param>
    /// <param name="val">The string value to write.</param>
    /// <param name="okToLog">Whether to write a log record for this change.</param>
    public void SetString(BlockId blk, int offset, string val, bool okToLog)
    {
        _concurMgr.XLock(blk);
        Buffer buff = _myBuffers.GetBuffer(blk);
        int lsn = -1;
        if (okToLog)
            lsn = _recoveryMgr.SetString(buff, offset, val);
        Page p = buff.Contents();
        p.SetString(offset, val);
        buff.SetModified(_txnum, lsn);
    }

    /// <summary>
    /// Returns the number of blocks in the specified file, after acquiring a shared lock
    /// on the end-of-file sentinel block.
    /// </summary>
    /// <param name="filename">The name of the file.</param>
    public int Size(string filename)
    {
        // locking the EOF block 
        // to avoid phantoms
        BlockId dummyBlk = new BlockId(filename, END_OF_FILE);
        _concurMgr.SLock(dummyBlk);
        return _fm.Length(filename);
    }

    /// <summary>
    /// Appends a new block to the specified file after acquiring an exclusive lock
    /// on the end-of-file sentinel block.
    /// </summary>
    /// <param name="filename">The name of the file to extend.</param>
    /// <returns>The <see cref="BlockId"/> of the newly appended block.</returns>
    public BlockId Append(string filename)
    {
        // locking the EOF block 
        // to avoid phantoms
        BlockId dummyBlk = new BlockId(filename, END_OF_FILE);
        _concurMgr.XLock(dummyBlk);
        int lsn = -1;
        lsn = _recoveryMgr.Append(filename);
        return _fm.Append(filename);
    }

    /// <summary>
    /// Removes the last block from the specified file.
    /// Used during recovery to undo an append operation.
    /// No lock is acquired here because this is only called during rollback
    /// (where the transaction already holds an XLock on the EOF block) or
    /// crash recovery (where a single transaction processes undos sequentially
    /// with no concurrent access).
    /// </summary>
    /// <param name="filename">The name of the file to truncate.</param>
    public void Truncate(string filename)
    {
        _fm.Truncate(filename);
    }

    /// <summary>
    /// Returns the block size used by the underlying file manager.
    /// </summary>
    public int BlockSize()
    {
        return _fm.BlockSize();
    }

    /// <summary>
    /// Returns the number of available (unpinned) buffers in the buffer pool.
    /// </summary>
    public int AvailableBuffs()
    {
        return _bm.Available();
    }

    private static int NextTxNumber()
    {
        lock (_locker)
        {
            _nextTxNum++;
            Console.WriteLine("new transaction: " + _nextTxNum);
            return _nextTxNum;
        }
    }

    /// <summary>
    /// Runs a quiescent checkpoint: blocks new transactions, waits for all running
    /// transactions to finish, flushes all modified buffers, and writes a CHECKPOINT
    /// log record.
    /// </summary>
    /// <param name="bm">The buffer manager used to flush dirty buffers.</param>
    /// <param name="lm">The log manager used to write the checkpoint record.</param>
    public static void RunQuiescentCheckpointing(IBufferMgr bm, LogMgr lm)
    {
        // stop accepting new transactions
        StartCheckpoint();
        try
        {
            // wait for existing transactions to finish
            while (!_runningTxns.IsEmpty)
            {
                Thread.Sleep(1);
            }
            // flush all modified buffers
            bm.FlushAll();
            // append a quiescent checkpoint record to the log and flush it to disk
            int lsn = CheckpointRecord.WriteToLog(lm);
            lm.Flush(lsn);
        }
        finally
        {
            // start accepting new transactions
            EndCheckpoint();
        }
    }
}
