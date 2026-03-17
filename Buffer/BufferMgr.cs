using DBSharp.File;
using DBSharp.Log;

namespace DBSharp.Buffer;

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
            // replace with new block 
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
            // find unpinned buffer
            buff = ChooseUnpinnedBuffer();
            if (buff == null)
                return null;
            // replace with new block 
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

        return (candidate >= 0) ? _bufferpool[candidate] : null;
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