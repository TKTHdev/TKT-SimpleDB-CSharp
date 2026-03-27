using DBSharp.File;
using DBSharp.Log;

namespace DBSharp.Buffers;

public class BufferMgr
{
    private Buffer[] _bufferpool;
    private int _numAvailable;
    private static  readonly long MAX_TIME = 10000;//10 seconds

    public BufferMgr(FileMgr fm, LogMgr lm, int numbuffs)
    {
        _bufferpool = new Buffer[numbuffs];
        // when initialized, all buffers are available
        _numAvailable = numbuffs;
        for(int i=0;i<numbuffs;i++)
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
            // flush all the buffer in buffer pool 
            // with corresponding txn id
            foreach (Buffer buff in _bufferpool)
            {
                if (buff.ModifyingTxn() == txnum)
                    buff.Flush();
            }
        }
    }

    public Buffer Pin(BlockId blk)
    {
        lock (this)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Buffer buff = tryToPin(blk);
            // wait until it can acquire lock
            // MAX_TIME is the maximum time it waits 
            while (buff == null && !WaitingTooLong(timestamp))
            {
                Monitor.Wait(this, TimeSpan.FromMilliseconds(MAX_TIME));
                buff = tryToPin(blk);
            }

            if (buff == null)
                throw new BufferAbortException();
            return buff;
        }
    }

    private Buffer? tryToPin(BlockId blk)
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
public class FIFOBufferMgr
{
    private Buffer[] _bufferpool;
    // sequence number of each time the buffer was read in 
    // updated inside ChooseUnpinnedBuffer
    private long[] _seq_read_in;
    // this is incremented everytime it reads a block from disk to buffer
    private long _seq;
    private int _numAvailable;
    private static  readonly long MAX_TIME = 10000;//10 seconds

    public FIFOBufferMgr(FileMgr fm, LogMgr lm, int numbuffs)
    {
        _bufferpool = new Buffer[numbuffs];
        _seq_read_in = new long[numbuffs]; 
        // when initialized, all buffers are available
        _numAvailable = numbuffs;
        _seq = 0;
        for (int i = 0; i < numbuffs; i++)
        {
            _bufferpool[i] = new Buffer(fm, lm);
            _seq_read_in[i] = 0;
        }
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
            // flush all the buffer in buffer pool 
            // with corresponding txn id
            foreach (Buffer buff in _bufferpool)
            {
                if (buff.ModifyingTxn() == txnum)
                    buff.Flush();
            }
        }
    }

    public Buffer Pin(BlockId blk)
    {
        lock (this)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Buffer buff = tryToPin(blk);
            // wait until it can acquire lock
            // MAX_TIME is the maximum time it waits 
            while (buff == null && !WaitingTooLong(timestamp))
            {
                Monitor.Wait(this, TimeSpan.FromMilliseconds(MAX_TIME));
                buff = tryToPin(blk);
            }

            if (buff == null)
                throw new BufferAbortException();
            return buff;
        }
    }

    private Buffer? tryToPin(BlockId blk)
    {
        // try finding selected block in buffer pool
        // it might or might not be pinned
        Buffer? buff = FindExistingBuffer(blk);
        // if it cannot find the block in buffer pool
        // then search for unpinned buffer to evict
        if (buff == null)
        {
            // we need this later 
            // to update _seq_read_in[] 
            int bufindex = -1;
            // find unpinned buffer
            (buff, bufindex) = ChooseUnpinnedBuffer();
            if (buff == null)
                return null;
            // replace with new block.
            // AssignToBlock always calls Flush(), but actual write-back happens
            // only when the current buffer is dirty (txnum >= 0).
            buff.AssignToBlock(blk);
            _seq_read_in[bufindex] = ++_seq;
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

    private (Buffer?,int) ChooseUnpinnedBuffer()
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
                if (_seq_read_in[i] < oldestTime)
                {
                    oldestTime = _seq_read_in[i];
                    candidate = i;
                }
            }
        }
        return (candidate >= 0) ? (_bufferpool[candidate], candidate) : (null,-1);
    }

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

public class LRUBufferMgr
{
    private Buffer[] _bufferpool;
    private long[] _seq_unpinned;
    private long _seq;
    private int _numAvailable;
    private static  readonly long MAX_TIME = 10000;//10 seconds

    public LRUBufferMgr(FileMgr fm, LogMgr lm, int numbuffs)
    {
        _bufferpool = new Buffer[numbuffs];
        _seq_unpinned = new long[numbuffs];
        // when initialized, all buffers are available
        _numAvailable = numbuffs;
        _seq = 0;
        for (int i = 0; i < numbuffs; i++)
        {
            _bufferpool[i] = new Buffer(fm, lm);
            _seq_unpinned[i] = 0;
        }

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
            // flush all the buffer in buffer pool 
            // with corresponding txn id
            foreach (Buffer buff in _bufferpool)
            {
                if (buff.ModifyingTxn() == txnum)
                    buff.Flush();
            }
        }
    }

    public Buffer Pin(BlockId blk)
    {
        lock (this)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Buffer buff = tryToPin(blk);
            // wait until it can acquire lock
            // MAX_TIME is the maximum time it waits 
            while (buff == null && !WaitingTooLong(timestamp))
            {
                Monitor.Wait(this, TimeSpan.FromMilliseconds(MAX_TIME));
                buff = tryToPin(blk);
            }

            if (buff == null)
                throw new BufferAbortException();
            return buff;
        }
    }

    private Buffer? tryToPin(BlockId blk)
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
                if (_seq_unpinned[i] < oldestTime)
                {
                    oldestTime = _seq_unpinned[i];
                    candidate = i;
                }
            }
        }
        return (candidate >= 0) ? _bufferpool[candidate] : null;
    }

    public void Unpin(Buffer buff)
    {
        lock (this)
        {
            buff.Unpin();
            for (int i = 0; i < _bufferpool.Length; i++)
            {
                if (object.ReferenceEquals(_bufferpool[i], buff))
                {
                    _seq_unpinned[i] = ++_seq;
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

public class ClockBufferMgr
{
    private Buffer[] _bufferpool;
    private int _numAvailable;
    private static  readonly long MAX_TIME = 10000;//10 seconds
    private int _clock = 0;

    public ClockBufferMgr(FileMgr fm, LogMgr lm, int numbuffs)
    {
        _bufferpool = new Buffer[numbuffs];
        // when initialized, all buffers are available
        _numAvailable = numbuffs;
        for(int i=0;i<numbuffs;i++)
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
            // flush all the buffer in buffer pool 
            // with corresponding txn id
            foreach (Buffer buff in _bufferpool)
            {
                if (buff.ModifyingTxn() == txnum)
                    buff.Flush();
            }
        }
    }

    public Buffer Pin(BlockId blk)
    {
        lock (this)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Buffer buff = tryToPin(blk);
            // wait until it can acquire lock
            // MAX_TIME is the maximum time it waits 
            while (buff == null && !WaitingTooLong(timestamp))
            {
                Monitor.Wait(this, TimeSpan.FromMilliseconds(MAX_TIME));
                buff = tryToPin(blk);
            }

            if (buff == null)
                throw new BufferAbortException();
            return buff;
        }
    }

    private Buffer? tryToPin(BlockId blk)
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
        for(int offset = 0; offset<_bufferpool.Length; offset++)
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

// buffer manager with a page replacement strategy
// that chooses unmodified pages over modified ones.
public class CleanFirstBufferMgr
{
    private Buffer[] _bufferpool;
    private int _numAvailable;
    private static  readonly long MAX_TIME = 10000;//10 seconds

    public CleanFirstBufferMgr(FileMgr fm, LogMgr lm, int numbuffs)
    {
        _bufferpool = new Buffer[numbuffs];
        // when initialized, all buffers are available
        _numAvailable = numbuffs;
        for(int i=0;i<numbuffs;i++)
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
            // flush all the buffer in buffer pool 
            // with corresponding txn id
            foreach (Buffer buff in _bufferpool)
            {
                if (buff.ModifyingTxn() == txnum)
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

// buffer manager with a page replacement strategy
// that chooses the modified page having the lowest LSN
public class LSNBasedBufferMgr
{
    private Buffer[] _bufferpool;
    private int _numAvailable;
    private static  readonly long MAX_TIME = 10000;//10 seconds

    public LSNBasedBufferMgr(FileMgr fm, LogMgr lm, int numbuffs)
    {
        _bufferpool = new Buffer[numbuffs];
        // when initialized, all buffers are available
        _numAvailable = numbuffs;
        for(int i=0;i<numbuffs;i++)
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
            // flush all the buffer in buffer pool 
            // with corresponding txn id
            foreach (Buffer buff in _bufferpool)
            {
                if (buff.ModifyingTxn() == txnum)
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

public class BufferMgrWithBufferHashTable
{
    private Buffer[] _bufferpool;
    private int _numAvailable;
    private static  readonly long MAX_TIME = 10000;//10 seconds
    private Dictionary<BlockId, Buffer> _hashtable = new Dictionary<BlockId, Buffer>();

    public BufferMgrWithBufferHashTable(FileMgr fm, LogMgr lm, int numbuffs)
    {
        _bufferpool = new Buffer[numbuffs];
        // when initialized, all buffers are available
        _numAvailable = numbuffs;
        for(int i=0;i<numbuffs;i++)
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
            // flush all the buffer in buffer pool 
            // with corresponding txn id
            foreach (Buffer buff in _bufferpool)
            {
                if (buff.ModifyingTxn() == txnum)
                    buff.Flush();
            }
        }
    }

    public Buffer Pin(BlockId blk)
    {
        lock (this)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Buffer buff = tryToPin(blk);
            // wait until it can acquire lock
            // MAX_TIME is the maximum time it waits 
            while (buff == null && !WaitingTooLong(timestamp))
            {
                Monitor.Wait(this, TimeSpan.FromMilliseconds(MAX_TIME));
                buff = tryToPin(blk);
            }

            if (buff == null)
                throw new BufferAbortException();
            return buff;
        }
    }

    private Buffer? tryToPin(BlockId blk)
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
            Buffer buff  = _hashtable[blk];
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
