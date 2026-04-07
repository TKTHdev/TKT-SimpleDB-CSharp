using DBSharp.File;
using DBSharp.Log;

namespace DBSharp.Buffers;

/// <summary>
/// Manages a fixed-size buffer pool using a naive replacement strategy that picks
/// the first unpinned buffer found.
/// </summary>
public class BufferMgr
{
    private Buffer[] _bufferpool;
    private int _numAvailable;
    private static readonly long MAX_TIME = 10000;//10 seconds

    /// <summary>
    /// Creates a buffer manager with the specified number of buffer slots.
    /// </summary>
    /// <param name="fm">The file manager for disk I/O.</param>
    /// <param name="lm">The log manager for WAL operations.</param>
    /// <param name="numbuffs">The number of buffers in the pool.</param>
    public BufferMgr(FileMgr fm, LogMgr lm, int numbuffs)
    {
        _bufferpool = new Buffer[numbuffs];
        // when initialized, all buffers are available
        _numAvailable = numbuffs;
        for (int i = 0; i < numbuffs; i++)
            _bufferpool[i] = new Buffer(fm, lm);
    }

    /// <summary>
    /// Returns the number of unpinned (available) buffers.
    /// </summary>
    public int Available()
    {
        lock (this)
        {
            return _numAvailable;
        }
    }

    /// <summary>
    /// Flushes all dirty buffers that were modified by the specified transaction.
    /// </summary>
    /// <param name="txnum">The transaction number whose buffers should be flushed.</param>
    public void FlushAll(int txnum)
    {
        lock (this)
        {
            // flush all the buffer in buffer pool
            // with corresponding txn id
            foreach (Buffer buff in _bufferpool)
            {
                if (buff.ModifyingTxn() == txnum)
                    buff.Flush();
            }
        }
    }

    /// <summary>
    /// Pins the buffer holding the specified block. If the block is not already in the pool,
    /// an unpinned buffer is chosen for replacement. Blocks until a buffer is available or
    /// the wait times out, throwing <see cref="BufferAbortException"/>.
    /// </summary>
    /// <param name="blk">The block to pin.</param>
    /// <returns>The pinned buffer.</returns>
    /// <exception cref="BufferAbortException">Thrown if no buffer becomes available within the timeout.</exception>
    public Buffer Pin(BlockId blk)
    {
        lock (this)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Buffer buff = TryToPin(blk);
            // wait until it can acquire lock
            // MAX_TIME is the maximum time it waits
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

    private Buffer? TryToPin(BlockId blk)
    {
        // try finding selected block in buffer pool
        // it might or might not be pinned
        Buffer? buff = FindExistingBuffer(blk);
        // if it cannot find the block in buffer pool
        // then search for unpinned buffer to evict
        if (buff == null)
        {
            // find unpinned buffer
            buff = ChooseUnpinnedBuffer();
            if (buff == null)
                return null;
            // replace with new block.
            // AssignToBlock always calls Flush(), but actual write-back happens
            // only when the current buffer is dirty (txnum >= 0).
            buff.AssignToBlock(blk);
        }
        // when it is newly pinned block
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

    private Buffer? FindExistingBuffer(BlockId blk)
    {
        foreach (Buffer buff in _bufferpool)
        {
            BlockId b = buff.Block();
            if (b != null && b.Equals(blk))
                return buff;
        }
        return null;
    }

    private Buffer? ChooseUnpinnedBuffer()
    {
        foreach (Buffer buff in _bufferpool)
        {
            if (buff.IsPinned() == false)
                return buff;
        }
        return null;
    }

    /// <summary>
    /// Unpins the given buffer. If the buffer becomes fully unpinned, it is made available
    /// for replacement and all waiting threads are notified.
    /// </summary>
    /// <param name="buff">The buffer to unpin.</param>
    public void Unpin(Buffer buff)
    {
        lock (this)
        {
            buff.Unpin();
            if (buff.IsPinned() == false)
            {
                _numAvailable++;
                // notify all the threads waiting with Monitor.Wait
                Monitor.PulseAll(this);
            }
        }
    }
}

/// <summary>
/// Buffer manager that uses a FIFO (First-In, First-Out) replacement strategy.
/// Evicts the buffer whose block was loaded into the pool earliest.
/// </summary>
public class FIFOBufferMgr
{
    private Buffer[] _bufferpool;
    // sequence number of each time the buffer was read in
    // updated inside ChooseUnpinnedBuffer
    private long[] _seqReadIn;
    // this is incremented everytime it reads a block from disk to buffer
    private long _seq;
    private int _numAvailable;
    private static readonly long MAX_TIME = 10000;//10 seconds

    /// <summary>
    /// Creates a FIFO buffer manager with the specified number of buffer slots.
    /// </summary>
    /// <param name="fm">The file manager for disk I/O.</param>
    /// <param name="lm">The log manager for WAL operations.</param>
    /// <param name="numbuffs">The number of buffers in the pool.</param>
    public FIFOBufferMgr(FileMgr fm, LogMgr lm, int numbuffs)
    {
        _bufferpool = new Buffer[numbuffs];
        _seqReadIn = new long[numbuffs];
        // when initialized, all buffers are available
        _numAvailable = numbuffs;
        _seq = 0;
        for (int i = 0; i < numbuffs; i++)
        {
            _bufferpool[i] = new Buffer(fm, lm);
            _seqReadIn[i] = 0;
        }
    }

    /// <summary>
    /// Returns the number of unpinned (available) buffers.
    /// </summary>
    public int Available()
    {
        lock (this)
        {
            return _numAvailable;
        }
    }

    /// <summary>
    /// Flushes all dirty buffers that were modified by the specified transaction.
    /// </summary>
    /// <param name="txnum">The transaction number whose buffers should be flushed.</param>
    public void FlushAll(int txnum)
    {
        lock (this)
        {
            // flush all the buffer in buffer pool
            // with corresponding txn id
            foreach (Buffer buff in _bufferpool)
            {
                if (buff.ModifyingTxn() == txnum)
                    buff.Flush();
            }
        }
    }

    /// <summary>
    /// Pins the buffer holding the specified block, using FIFO eviction if needed.
    /// </summary>
    /// <param name="blk">The block to pin.</param>
    /// <returns>The pinned buffer.</returns>
    /// <exception cref="BufferAbortException">Thrown if no buffer becomes available within the timeout.</exception>
    public Buffer Pin(BlockId blk)
    {
        lock (this)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Buffer buff = TryToPin(blk);
            // wait until it can acquire lock
            // MAX_TIME is the maximum time it waits
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

    private Buffer? TryToPin(BlockId blk)
    {
        // try finding selected block in buffer pool
        // it might or might not be pinned
        Buffer? buff = FindExistingBuffer(blk);
        // if it cannot find the block in buffer pool
        // then search for unpinned buffer to evict
        if (buff == null)
        {
            // we need this later
            // to update _seqReadIn[]
            int bufindex = -1;
            // find unpinned buffer
            (buff, bufindex) = ChooseUnpinnedBuffer();
            if (buff == null)
                return null;
            // replace with new block.
            // AssignToBlock always calls Flush(), but actual write-back happens
            // only when the current buffer is dirty (txnum >= 0).
            buff.AssignToBlock(blk);
            _seqReadIn[bufindex] = ++_seq;
        }
        // when it is newly pinned block
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

    private Buffer? FindExistingBuffer(BlockId blk)
    {
        foreach (Buffer buff in _bufferpool)
        {
            BlockId b = buff.Block();
            if (b != null && b.Equals(blk))
                return buff;
        }
        return null;
    }

    private (Buffer?, int) ChooseUnpinnedBuffer()
    {
        // First, consume truly free frames before evicting existing blocks.
        for (int i = 0; i < _bufferpool.Length; i++)
        {
            if (!_bufferpool[i].IsPinned() && _bufferpool[i].Block() == null)
                return (_bufferpool[i], i);
        }

        // If all frames have been used, pick the oldest loaded unpinned frame (FIFO).
        int candidate = -1;
        long oldestTime = long.MaxValue;
        for (int i = 0; i < _bufferpool.Length; i++)
        {
            if (!_bufferpool[i].IsPinned())
            {
                if (_seqReadIn[i] < oldestTime)
                {
                    oldestTime = _seqReadIn[i];
                    candidate = i;
                }
            }
        }
        return (candidate >= 0) ? (_bufferpool[candidate], candidate) : (null, -1);
    }

    /// <summary>
    /// Unpins the given buffer and notifies waiting threads if it becomes fully unpinned.
    /// </summary>
    /// <param name="buff">The buffer to unpin.</param>
    public void Unpin(Buffer buff)
    {
        lock (this)
        {
            buff.Unpin();
            if (buff.IsPinned() == false)
            {
                _numAvailable++;
                // notify all the threads waiting with Monitor.Wait
                Monitor.PulseAll(this);
            }
        }
    }
}

/// <summary>
/// Buffer manager that uses an LRU (Least Recently Used) replacement strategy.
/// Evicts the unpinned buffer that was unpinned the longest ago.
/// </summary>
public class LRUBufferMgr
{
    private Buffer[] _bufferpool;
    private long[] _seqUnpinned;
    private long _seq;
    private int _numAvailable;
    private static readonly long MAX_TIME = 10000;//10 seconds

    /// <summary>
    /// Creates an LRU buffer manager with the specified number of buffer slots.
    /// </summary>
    /// <param name="fm">The file manager for disk I/O.</param>
    /// <param name="lm">The log manager for WAL operations.</param>
    /// <param name="numbuffs">The number of buffers in the pool.</param>
    public LRUBufferMgr(FileMgr fm, LogMgr lm, int numbuffs)
    {
        _bufferpool = new Buffer[numbuffs];
        _seqUnpinned = new long[numbuffs];
        // when initialized, all buffers are available
        _numAvailable = numbuffs;
        _seq = 0;
        for (int i = 0; i < numbuffs; i++)
        {
            _bufferpool[i] = new Buffer(fm, lm);
            _seqUnpinned[i] = 0;
        }

    }

    /// <summary>
    /// Returns the number of unpinned (available) buffers.
    /// </summary>
    public int Available()
    {
        lock (this)
        {
            return _numAvailable;
        }
    }

    /// <summary>
    /// Flushes all dirty buffers that were modified by the specified transaction.
    /// </summary>
    /// <param name="txnum">The transaction number whose buffers should be flushed.</param>
    public void FlushAll(int txnum)
    {
        lock (this)
        {
            // flush all the buffer in buffer pool
            // with corresponding txn id
            foreach (Buffer buff in _bufferpool)
            {
                if (buff.ModifyingTxn() == txnum)
                    buff.Flush();
            }
        }
    }

    /// <summary>
    /// Pins the buffer holding the specified block, using LRU eviction if needed.
    /// </summary>
    /// <param name="blk">The block to pin.</param>
    /// <returns>The pinned buffer.</returns>
    /// <exception cref="BufferAbortException">Thrown if no buffer becomes available within the timeout.</exception>
    public Buffer Pin(BlockId blk)
    {
        lock (this)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Buffer buff = TryToPin(blk);
            // wait until it can acquire lock
            // MAX_TIME is the maximum time it waits
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

    private Buffer? TryToPin(BlockId blk)
    {
        // try finding selected block in buffer pool
        // it might or might not be pinned
        Buffer? buff = FindExistingBuffer(blk);
        // if it cannot find the block in buffer pool
        // then search for unpinned buffer to evict
        if (buff == null)
        {
            // find unpinned buffer
            buff = ChooseUnpinnedBuffer();
            if (buff == null)
                return null;
            // replace with new block.
            // AssignToBlock always calls Flush(), but actual write-back happens
            // only when the current buffer is dirty (txnum >= 0).
            buff.AssignToBlock(blk);
        }
        // when it is newly pinned block
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

    private Buffer? FindExistingBuffer(BlockId blk)
    {
        foreach (Buffer buff in _bufferpool)
        {
            BlockId b = buff.Block();
            if (b != null && b.Equals(blk))
                return buff;
        }
        return null;
    }


    private Buffer? ChooseUnpinnedBuffer()
    {
        // First, consume truly free frames before evicting existing blocks.
        for (int i = 0; i < _bufferpool.Length; i++)
        {
            if (!_bufferpool[i].IsPinned() && _bufferpool[i].Block() == null)
                return _bufferpool[i];
        }

        // If all frames have been used, pick the oldest loaded unpinned frame (FIFO).
        int candidate = -1;
        long oldestTime = long.MaxValue;
        for (int i = 0; i < _bufferpool.Length; i++)
        {
            if (!_bufferpool[i].IsPinned())
            {
                if (_seqUnpinned[i] < oldestTime)
                {
                    oldestTime = _seqUnpinned[i];
                    candidate = i;
                }
            }
        }
        return (candidate >= 0) ? _bufferpool[candidate] : null;
    }

    /// <summary>
    /// Unpins the given buffer, records the unpin sequence for LRU tracking,
    /// and notifies waiting threads if it becomes fully unpinned.
    /// </summary>
    /// <param name="buff">The buffer to unpin.</param>
    public void Unpin(Buffer buff)
    {
        lock (this)
        {
            buff.Unpin();
            for (int i = 0; i < _bufferpool.Length; i++)
            {
                if (object.ReferenceEquals(_bufferpool[i], buff))
                {
                    _seqUnpinned[i] = ++_seq;
                    break;
                }
            }

            if (buff.IsPinned() == false)
            {
                _numAvailable++;
                // notify all the threads waiting with Monitor.Wait
                Monitor.PulseAll(this);
            }
        }
    }
}

/// <summary>
/// Buffer manager that uses a clock (second-chance) replacement strategy.
/// Cycles through buffers in round-robin order, choosing the first unpinned one.
/// </summary>
public class ClockBufferMgr
{
    private Buffer[] _bufferpool;
    private int _numAvailable;
    private static readonly long MAX_TIME = 10000;//10 seconds
    private int _clock = 0;

    /// <summary>
    /// Creates a clock buffer manager with the specified number of buffer slots.
    /// </summary>
    /// <param name="fm">The file manager for disk I/O.</param>
    /// <param name="lm">The log manager for WAL operations.</param>
    /// <param name="numbuffs">The number of buffers in the pool.</param>
    public ClockBufferMgr(FileMgr fm, LogMgr lm, int numbuffs)
    {
        _bufferpool = new Buffer[numbuffs];
        // when initialized, all buffers are available
        _numAvailable = numbuffs;
        for (int i = 0; i < numbuffs; i++)
            _bufferpool[i] = new Buffer(fm, lm);
    }

    /// <summary>
    /// Returns the number of unpinned (available) buffers.
    /// </summary>
    public int Available()
    {
        lock (this)
        {
            return _numAvailable;
        }
    }

    /// <summary>
    /// Flushes all dirty buffers that were modified by the specified transaction.
    /// </summary>
    /// <param name="txnum">The transaction number whose buffers should be flushed.</param>
    public void FlushAll(int txnum)
    {
        lock (this)
        {
            // flush all the buffer in buffer pool
            // with corresponding txn id
            foreach (Buffer buff in _bufferpool)
            {
                if (buff.ModifyingTxn() == txnum)
                    buff.Flush();
            }
        }
    }

    /// <summary>
    /// Pins the buffer holding the specified block, using clock eviction if needed.
    /// </summary>
    /// <param name="blk">The block to pin.</param>
    /// <returns>The pinned buffer.</returns>
    /// <exception cref="BufferAbortException">Thrown if no buffer becomes available within the timeout.</exception>
    public Buffer Pin(BlockId blk)
    {
        lock (this)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Buffer buff = TryToPin(blk);
            // wait until it can acquire lock
            // MAX_TIME is the maximum time it waits
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

    private Buffer? TryToPin(BlockId blk)
    {
        // try finding selected block in buffer pool
        // it might or might not be pinned
        Buffer? buff = FindExistingBuffer(blk);
        // if it cannot find the block in buffer pool
        // then search for unpinned buffer to evict
        if (buff == null)
        {
            // find unpinned buffer
            buff = ChooseUnpinnedBuffer();
            if (buff == null)
                return null;
            // replace with new block.
            // AssignToBlock always calls Flush(), but actual write-back happens
            // only when the current buffer is dirty (txnum >= 0).
            buff.AssignToBlock(blk);
        }
        // when it is newly pinned block
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

    private Buffer? FindExistingBuffer(BlockId blk)
    {
        foreach (Buffer buff in _bufferpool)
        {
            BlockId b = buff.Block();
            if (b != null && b.Equals(blk))
                return buff;
        }
        return null;
    }

    private Buffer? ChooseUnpinnedBuffer()
    {
        for (int offset = 0; offset < _bufferpool.Length; offset++)
        {
            int idx = (_clock + offset) % _bufferpool.Length;
            Buffer buff = _bufferpool[idx];
            if (buff.IsPinned() == false)
            {
                _clock = (idx + 1) % _bufferpool.Length;
                return buff;
            }
        }
        return null;
    }

    /// <summary>
    /// Unpins the given buffer and notifies waiting threads if it becomes fully unpinned.
    /// </summary>
    /// <param name="buff">The buffer to unpin.</param>
    public void Unpin(Buffer buff)
    {
        lock (this)
        {
            buff.Unpin();
            if (buff.IsPinned() == false)
            {
                _numAvailable++;
                // notify all the threads waiting with Monitor.Wait
                Monitor.PulseAll(this);
            }
        }
    }
}

/// <summary>
/// Buffer manager that prefers to evict clean (unmodified) pages over dirty ones,
/// reducing the number of disk writes needed during replacement.
/// </summary>
public class CleanFirstBufferMgr
{
    private Buffer[] _bufferpool;
    private int _numAvailable;
    private static readonly long MAX_TIME = 10000;//10 seconds

    /// <summary>
    /// Creates a clean-first buffer manager with the specified number of buffer slots.
    /// </summary>
    /// <param name="fm">The file manager for disk I/O.</param>
    /// <param name="lm">The log manager for WAL operations.</param>
    /// <param name="numbuffs">The number of buffers in the pool.</param>
    public CleanFirstBufferMgr(FileMgr fm, LogMgr lm, int numbuffs)
    {
        _bufferpool = new Buffer[numbuffs];
        // when initialized, all buffers are available
        _numAvailable = numbuffs;
        for (int i = 0; i < numbuffs; i++)
            _bufferpool[i] = new Buffer(fm, lm);
    }

    /// <summary>
    /// Returns the number of unpinned (available) buffers.
    /// </summary>
    public int Available()
    {
        lock (this)
        {
            return _numAvailable;
        }
    }

    /// <summary>
    /// Flushes all dirty buffers that were modified by the specified transaction.
    /// </summary>
    /// <param name="txnum">The transaction number whose buffers should be flushed.</param>
    public void FlushAll(int txnum)
    {
        lock (this)
        {
            // flush all the buffer in buffer pool
            // with corresponding txn id
            foreach (Buffer buff in _bufferpool)
            {
                if (buff.ModifyingTxn() == txnum)
                    buff.Flush();
            }
        }
    }

    /// <summary>
    /// Pins the buffer holding the specified block, preferring clean-page eviction.
    /// </summary>
    /// <param name="blk">The block to pin.</param>
    /// <returns>The pinned buffer.</returns>
    /// <exception cref="BufferAbortException">Thrown if no buffer becomes available within the timeout.</exception>
    public Buffer Pin(BlockId blk)
    {
        lock (this)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Buffer buff = TryToPin(blk);
            // wait until it can acquire lock
            // MAX_TIME is the maximum time it waits
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

    private Buffer? TryToPin(BlockId blk)
    {
        // try finding selected block in buffer pool
        // it might or might not be pinned
        Buffer? buff = FindExistingBuffer(blk);
        // if it cannot find the block in buffer pool
        // then search for unpinned buffer to evict
        if (buff == null)
        {
            // find unpinned buffer
            buff = ChooseReplacementBuffer();
            if (buff == null)
                return null;
            // replace with new block.
            // AssignToBlock always calls Flush(), but actual write-back happens
            // only when the current buffer is dirty (txnum >= 0).
            buff.AssignToBlock(blk);
        }
        // when it is newly pinned block
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

    private Buffer? FindExistingBuffer(BlockId blk)
    {
        foreach (Buffer buff in _bufferpool)
        {
            BlockId b = buff.Block();
            if (b != null && b.Equals(blk))
                return buff;
        }
        return null;
    }

    private Buffer? ChooseReplacementBuffer()
    {
        foreach (Buffer buff in _bufferpool)
        {
            bool isClean = buff.ModifyingTxn() == -1;
            if (buff.IsPinned() == false && isClean)
                return buff;
        }
        // if no clean frame is found, fall back to any unpinned frame
        foreach (Buffer buff in _bufferpool)
        {
            if (buff.IsPinned() == false)
                return buff;
        }
        return null;
    }

    /// <summary>
    /// Unpins the given buffer and notifies waiting threads if it becomes fully unpinned.
    /// </summary>
    /// <param name="buff">The buffer to unpin.</param>
    public void Unpin(Buffer buff)
    {
        lock (this)
        {
            buff.Unpin();
            if (buff.IsPinned() == false)
            {
                _numAvailable++;
                // notify all the threads waiting with Monitor.Wait
                Monitor.PulseAll(this);
            }
        }
    }
}

/// <summary>
/// Buffer manager that evicts the dirty page with the lowest LSN first, minimizing
/// the amount of log that must be flushed before write-back. Clean pages are preferred
/// over dirty ones.
/// </summary>
public class LSNBasedBufferMgr
{
    private Buffer[] _bufferpool;
    private int _numAvailable;
    private static readonly long MAX_TIME = 10000;//10 seconds

    /// <summary>
    /// Creates an LSN-based buffer manager with the specified number of buffer slots.
    /// </summary>
    /// <param name="fm">The file manager for disk I/O.</param>
    /// <param name="lm">The log manager for WAL operations.</param>
    /// <param name="numbuffs">The number of buffers in the pool.</param>
    public LSNBasedBufferMgr(FileMgr fm, LogMgr lm, int numbuffs)
    {
        _bufferpool = new Buffer[numbuffs];
        // when initialized, all buffers are available
        _numAvailable = numbuffs;
        for (int i = 0; i < numbuffs; i++)
            _bufferpool[i] = new Buffer(fm, lm);
    }

    /// <summary>
    /// Returns the number of unpinned (available) buffers.
    /// </summary>
    public int Available()
    {
        lock (this)
        {
            return _numAvailable;
        }
    }

    /// <summary>
    /// Flushes all dirty buffers that were modified by the specified transaction.
    /// </summary>
    /// <param name="txnum">The transaction number whose buffers should be flushed.</param>
    public void FlushAll(int txnum)
    {
        lock (this)
        {
            // flush all the buffer in buffer pool
            // with corresponding txn id
            foreach (Buffer buff in _bufferpool)
            {
                if (buff.ModifyingTxn() == txnum)
                    buff.Flush();
            }
        }
    }

    /// <summary>
    /// Pins the buffer holding the specified block, using LSN-based eviction if needed.
    /// </summary>
    /// <param name="blk">The block to pin.</param>
    /// <returns>The pinned buffer.</returns>
    /// <exception cref="BufferAbortException">Thrown if no buffer becomes available within the timeout.</exception>
    public Buffer Pin(BlockId blk)
    {
        lock (this)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Buffer buff = TryToPin(blk);
            // wait until it can acquire lock
            // MAX_TIME is the maximum time it waits
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

    private Buffer? TryToPin(BlockId blk)
    {
        // try finding selected block in buffer pool
        // it might or might not be pinned
        Buffer? buff = FindExistingBuffer(blk);
        // if it cannot find the block in buffer pool
        // then search for unpinned buffer to evict
        if (buff == null)
        {
            // find unpinned buffer
            buff = ChooseReplacementBuffer();
            if (buff == null)
                return null;
            // replace with new block.
            // AssignToBlock always calls Flush(), but actual write-back happens
            // only when the current buffer is dirty (txnum >= 0).
            buff.AssignToBlock(blk);
        }
        // when it is newly pinned block
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

    private Buffer? FindExistingBuffer(BlockId blk)
    {
        foreach (Buffer buff in _bufferpool)
        {
            BlockId b = buff.Block();
            if (b != null && b.Equals(blk))
                return buff;
        }
        return null;
    }

    private Buffer? ChooseReplacementBuffer()
    {
        foreach (Buffer buff in _bufferpool)
        {
            bool isClean = buff.ModifyingTxn() == -1;
            if (buff.IsPinned() == false && isClean)
                return buff;
        }
        // if no clean frame is found, fall back to any unpinned frame
        // get the buffer with lowest LSN
        int lowestLSN = int.MaxValue;
        Buffer bufWithLowestLSN = null;
        foreach (Buffer buff in _bufferpool)
        {
            // get the buffer with lowest LSN
            if (buff.IsPinned() == false)
            {
                if (buff.GetLSN() < lowestLSN)
                {
                    lowestLSN = buff.GetLSN();
                    bufWithLowestLSN = buff;
                }
            }
        }
        return bufWithLowestLSN;
    }

    /// <summary>
    /// Unpins the given buffer and notifies waiting threads if it becomes fully unpinned.
    /// </summary>
    /// <param name="buff">The buffer to unpin.</param>
    public void Unpin(Buffer buff)
    {
        lock (this)
        {
            buff.Unpin();
            if (buff.IsPinned() == false)
            {
                _numAvailable++;
                // notify all the threads waiting with Monitor.Wait
                Monitor.PulseAll(this);
            }
        }
    }
}

/// <summary>
/// Buffer manager that maintains a hash table mapping <see cref="BlockId"/> to buffers
/// for O(1) lookup of blocks already in the pool.
/// </summary>
public class BufferMgrWithBufferHashTable
{
    private Buffer[] _bufferpool;
    private int _numAvailable;
    private static readonly long MAX_TIME = 10000;//10 seconds
    private Dictionary<BlockId, Buffer> _hashtable = new Dictionary<BlockId, Buffer>();

    /// <summary>
    /// Creates a hash-table-backed buffer manager with the specified number of buffer slots.
    /// </summary>
    /// <param name="fm">The file manager for disk I/O.</param>
    /// <param name="lm">The log manager for WAL operations.</param>
    /// <param name="numbuffs">The number of buffers in the pool.</param>
    public BufferMgrWithBufferHashTable(FileMgr fm, LogMgr lm, int numbuffs)
    {
        _bufferpool = new Buffer[numbuffs];
        // when initialized, all buffers are available
        _numAvailable = numbuffs;
        for (int i = 0; i < numbuffs; i++)
            _bufferpool[i] = new Buffer(fm, lm);
    }

    /// <summary>
    /// Returns the number of unpinned (available) buffers.
    /// </summary>
    public int Available()
    {
        lock (this)
        {
            return _numAvailable;
        }
    }

    /// <summary>
    /// Flushes all dirty buffers that were modified by the specified transaction.
    /// </summary>
    /// <param name="txnum">The transaction number whose buffers should be flushed.</param>
    public void FlushAll(int txnum)
    {
        lock (this)
        {
            // flush all the buffer in buffer pool
            // with corresponding txn id
            foreach (Buffer buff in _bufferpool)
            {
                if (buff.ModifyingTxn() == txnum)
                    buff.Flush();
            }
        }
    }

    /// <summary>
    /// Pins the buffer holding the specified block, using hash-table-accelerated lookup.
    /// </summary>
    /// <param name="blk">The block to pin.</param>
    /// <returns>The pinned buffer.</returns>
    /// <exception cref="BufferAbortException">Thrown if no buffer becomes available within the timeout.</exception>
    public Buffer Pin(BlockId blk)
    {
        lock (this)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Buffer buff = TryToPin(blk);
            // wait until it can acquire lock
            // MAX_TIME is the maximum time it waits
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

    private Buffer? TryToPin(BlockId blk)
    {
        // try finding selected block in buffer pool
        // it might or might not be pinned
        Buffer? buff = FindExistingBuffer(blk);
        // if it cannot find the block in buffer pool
        // then search for unpinned buffer to evict
        if (buff == null)
        {
            // find unpinned buffer
            buff = ChooseUnpinnedBuffer();
            if (buff == null)
                return null;
            // replace with new block.
            // AssignToBlock always calls Flush(), but actual write-back happens
            // only when the current buffer is dirty (txnum >= 0).
            BlockId oldBlk = buff.Block();
            if (oldBlk != null)
                _hashtable.Remove(oldBlk);
            buff.AssignToBlock(blk);
            _hashtable.Add(blk, buff);
        }
        // when it is newly pinned block
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

    private Buffer? FindExistingBuffer(BlockId blk)
    {
        if (_hashtable.ContainsKey(blk))
        {
            Buffer buff = _hashtable[blk];
            return buff;
        }

        foreach (Buffer buff in _bufferpool)
        {
            BlockId b = buff.Block();
            if (b != null && b.Equals(blk))
            {
                _hashtable.Add(blk, buff);
                return buff;
            }
        }
        return null;
    }

    private Buffer? ChooseUnpinnedBuffer()
    {
        foreach (Buffer buff in _bufferpool)
        {
            if (buff.IsPinned() == false)
                return buff;
        }
        return null;
    }

    /// <summary>
    /// Unpins the given buffer and notifies waiting threads if it becomes fully unpinned.
    /// </summary>
    /// <param name="buff">The buffer to unpin.</param>
    public void Unpin(Buffer buff)
    {
        lock (this)
        {
            buff.Unpin();
            if (buff.IsPinned() == false)
            {
                _numAvailable++;
                // notify all the threads waiting with Monitor.Wait
                Monitor.PulseAll(this);
            }
        }
    }
}
