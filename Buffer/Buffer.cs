using DBSharp.File;
using DBSharp.Log;

namespace DBSharp.Buffers;

/// <summary>
/// Represents a single buffer in the buffer pool. A buffer wraps an in-memory page
/// and tracks which disk block it currently holds, its pin count, and whether it has
/// been modified (dirty).
/// </summary>
public class Buffer
{
    private FileMgr _fm;
    private LogMgr _lm;
    private Page _contents;
    private BlockId _blk = null;
    private int _pins = 0;
    private int _txnum = -1;
    private int _lsn = -1;

    /// <summary>
    /// Creates a new buffer backed by a fresh page of the file manager's block size.
    /// </summary>
    /// <param name="fm">The file manager for disk I/O.</param>
    /// <param name="lm">The log manager for WAL flushing.</param>
    public Buffer(FileMgr fm, LogMgr lm)
    {
        _fm = fm;
        _lm = lm;
        _contents = new Page(fm.BlockSize());
    }

    /// <summary>
    /// Returns the page held by this buffer.
    /// </summary>
    public Page Contents()
    {
        return _contents;
    }

    /// <summary>
    /// Returns the block currently assigned to this buffer, or null if none.
    /// </summary>
    public BlockId Block()
    {
        return _blk;
    }

    /// <summary>
    /// Marks this buffer as modified by the given transaction. If the LSN is non-negative,
    /// it is recorded so the log can be flushed before the buffer is written back.
    /// </summary>
    /// <param name="txNum">The transaction number that modified this buffer.</param>
    /// <param name="lsn">The LSN of the corresponding log record, or -1 if not logged.</param>
    public void SetModified(int txNum, int lsn)
    {
        _txnum = txNum;
        if (lsn >= 0) _lsn = lsn;
    }

    /// <summary>
    /// Returns the transaction number of the transaction that last modified this buffer,
    /// or -1 if the buffer is clean.
    /// </summary>
    public int ModifyingTxn()
    {
        return _txnum;
    }

    /// <summary>
    /// Flushes the current contents (if dirty), then loads the specified block from disk
    /// and resets the pin count to zero.
    /// </summary>
    /// <param name="b">The block to load into this buffer.</param>
    public void AssignToBlock(BlockId b)
    {
        Flush();
        _blk = b;
        _fm.Read(_blk, _contents);
        _pins = 0;
    }

    /// <summary>
    /// If this buffer is dirty, flushes the log up to the recorded LSN and writes the
    /// page contents back to disk, then marks the buffer as clean.
    /// </summary>
    public void Flush()
    {
        if (_txnum >= 0)
        {
            _lm.Flush(_lsn);
            _fm.Write(_blk, _contents);
            _txnum = -1;
        }
    }

    /// <summary>
    /// Increments the pin count, indicating that a client is using this buffer.
    /// </summary>
    public void Pin()
    {
        _pins++;
    }

    /// <summary>
    /// Decrements the pin count.
    /// </summary>
    public void Unpin()
    {
        _pins--;
    }

    /// <summary>
    /// Returns whether this buffer is currently pinned by at least one client.
    /// </summary>
    public bool IsPinned()
    {
        return _pins > 0;
    }

    /// <summary>
    /// Returns the LSN of the most recent log record associated with this buffer's modification.
    /// </summary>
    public int GetLSN()
    {
        return _lsn;
    }
}
