using DBSharp.File;
using DBSharp.Log;

namespace DBSharp.Buffers;

/// <summary>
/// Base class for buffer managers. Provides the common pin/unpin/flush logic
/// and delegates the replacement decision to subclasses via template methods.
/// </summary>
public abstract class AbstractBufferMgr : IBufferMgr
{
    protected Buffer[] _bufferpool;
    private int _numAvailable;
    private static readonly long MAX_TIME = 10000; // 10 seconds

    protected AbstractBufferMgr(FileMgr fm, LogMgr lm, int numbuffs)
    {
        _bufferpool = new Buffer[numbuffs];
        _numAvailable = numbuffs;
        for (int i = 0; i < numbuffs; i++)
            _bufferpool[i] = new Buffer(fm, lm);
    }

    public int Available()
    {
        lock (this)
        {
            return _numAvailable;
        }
    }

    public void FlushAll(int txnum)
    {
        lock (this)
        {
            foreach (Buffer buff in _bufferpool)
            {
                if (buff.ModifyingTxn() == txnum)
                    buff.Flush();
            }
        }
    }

    public void FlushAll()
    {
        lock (this)
        {
            foreach (Buffer buff in _bufferpool)
            {
                if (buff.ModifyingTxn() >= 0)
                    buff.Flush();
            }
        }
    }

    public Buffer Pin(BlockId blk)
    {
        lock (this)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Buffer buff = TryToPin(blk);
            while (buff == null && !WaitingTooLong(timestamp))
            {
                Monitor.Wait(this, TimeSpan.FromMilliseconds(MAX_TIME));
                buff = TryToPin(blk);
            }

            if (buff == null)
                throw new BufferAbortException();
            return buff;
        }
    }

    public void Unpin(Buffer buff)
    {
        lock (this)
        {
            buff.Unpin();
            OnBufferUnpinned(buff);
            if (buff.IsPinned() == false)
            {
                _numAvailable++;
                Monitor.PulseAll(this);
            }
        }
    }

    private Buffer? TryToPin(BlockId blk)
    {
        Buffer? buff = FindExistingBuffer(blk);
        if (buff == null)
        {
            buff = ChooseUnpinnedBuffer();
            if (buff == null)
                return null;
            OnBufferReplaced(buff, blk);
            buff.AssignToBlock(blk);
        }
        if (buff.IsPinned() == false)
            _numAvailable--;
        buff.Pin();
        return buff;
    }

    private bool WaitingTooLong(long starttime)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return now - starttime > MAX_TIME;
    }

    /// <summary>
    /// Finds an existing buffer holding the specified block, or null if not in the pool.
    /// Override to use a faster lookup structure (e.g. hash table).
    /// </summary>
    protected virtual Buffer? FindExistingBuffer(BlockId blk)
    {
        foreach (Buffer buff in _bufferpool)
        {
            BlockId b = buff.Block();
            if (b != null && b.Equals(blk))
                return buff;
        }
        return null;
    }

    /// <summary>
    /// Selects an unpinned buffer for replacement. This is the primary extension point
    /// that defines the eviction strategy.
    /// </summary>
    protected abstract Buffer? ChooseUnpinnedBuffer();

    /// <summary>
    /// Called when a buffer is about to be replaced with a new block.
    /// Override to update replacement-policy metadata (e.g. FIFO sequence numbers).
    /// The default implementation does nothing.
    /// </summary>
    protected virtual void OnBufferReplaced(Buffer buff, BlockId newBlk) { }

    /// <summary>
    /// Called after a buffer has been unpinned (before checking if fully unpinned).
    /// Override to update replacement-policy metadata (e.g. LRU timestamps).
    /// The default implementation does nothing.
    /// </summary>
    protected virtual void OnBufferUnpinned(Buffer buff) { }
}
