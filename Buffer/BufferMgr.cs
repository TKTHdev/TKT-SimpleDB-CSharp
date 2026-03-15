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
        Buffer buff = FindExistingBuffer(blk);
        if (buff == null)
        {
            buff = ChooseUnpinnedBuffer();
            if (buff == null)
                return null;
            buff.AssignToBlock(blk);
        }

        if (buff.IsPinned() == false)
            _numAvailable--;
        buff.Pin();
        return buff;

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
                NotifyAll();
            }
        }
    }
}