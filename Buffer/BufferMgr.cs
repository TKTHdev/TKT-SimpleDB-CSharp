using DBSharp.File;
using DBSharp.Log;

namespace DBSharp.Buffers;

/// <summary>
/// Naive replacement strategy: picks the first unpinned buffer found.
/// </summary>
public class BufferMgr : AbstractBufferMgr
{
    public BufferMgr(FileMgr fm, LogMgr lm, int numbuffs) : base(fm, lm, numbuffs) { }

    protected override Buffer? ChooseUnpinnedBuffer()
    {
        foreach (Buffer buff in _bufferpool)
        {
            if (buff.IsPinned() == false)
                return buff;
        }
        return null;
    }
}

/// <summary>
/// FIFO replacement strategy: evicts the buffer whose block was loaded earliest.
/// </summary>
public class FIFOBufferMgr : AbstractBufferMgr
{
    private long[] _seqReadIn;
    private long _seq;

    public FIFOBufferMgr(FileMgr fm, LogMgr lm, int numbuffs) : base(fm, lm, numbuffs)
    {
        _seqReadIn = new long[numbuffs];
        _seq = 0;
    }

    protected override void OnBufferReplaced(Buffer buff, BlockId newBlk)
    {
        int idx = Array.IndexOf(_bufferpool, buff);
        if (idx >= 0)
            _seqReadIn[idx] = ++_seq;
    }

    protected override Buffer? ChooseUnpinnedBuffer()
    {
        // Prefer truly free frames first
        for (int i = 0; i < _bufferpool.Length; i++)
        {
            if (!_bufferpool[i].IsPinned() && _bufferpool[i].Block() == null)
                return _bufferpool[i];
        }

        // Otherwise pick the oldest loaded unpinned frame
        int candidate = -1;
        long oldestTime = long.MaxValue;
        for (int i = 0; i < _bufferpool.Length; i++)
        {
            if (!_bufferpool[i].IsPinned() && _seqReadIn[i] < oldestTime)
            {
                oldestTime = _seqReadIn[i];
                candidate = i;
            }
        }
        return candidate >= 0 ? _bufferpool[candidate] : null;
    }
}

/// <summary>
/// LRU replacement strategy: evicts the unpinned buffer that was unpinned longest ago.
/// </summary>
public class LRUBufferMgr : AbstractBufferMgr
{
    private long[] _seqUnpinned;
    private long _seq;

    public LRUBufferMgr(FileMgr fm, LogMgr lm, int numbuffs) : base(fm, lm, numbuffs)
    {
        _seqUnpinned = new long[numbuffs];
        _seq = 0;
    }

    protected override void OnBufferUnpinned(Buffer buff)
    {
        int idx = Array.IndexOf(_bufferpool, buff);
        if (idx >= 0)
            _seqUnpinned[idx] = ++_seq;
    }

    protected override Buffer? ChooseUnpinnedBuffer()
    {
        // Prefer truly free frames first
        for (int i = 0; i < _bufferpool.Length; i++)
        {
            if (!_bufferpool[i].IsPinned() && _bufferpool[i].Block() == null)
                return _bufferpool[i];
        }

        // Otherwise pick the least recently unpinned frame
        int candidate = -1;
        long oldestTime = long.MaxValue;
        for (int i = 0; i < _bufferpool.Length; i++)
        {
            if (!_bufferpool[i].IsPinned() && _seqUnpinned[i] < oldestTime)
            {
                oldestTime = _seqUnpinned[i];
                candidate = i;
            }
        }
        return candidate >= 0 ? _bufferpool[candidate] : null;
    }
}

/// <summary>
/// Clock (second-chance) replacement strategy: cycles through buffers in round-robin order.
/// </summary>
public class ClockBufferMgr : AbstractBufferMgr
{
    private int _clock = 0;

    public ClockBufferMgr(FileMgr fm, LogMgr lm, int numbuffs) : base(fm, lm, numbuffs) { }

    protected override Buffer? ChooseUnpinnedBuffer()
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
}

/// <summary>
/// Clean-first replacement strategy: prefers evicting clean (unmodified) pages.
/// </summary>
public class CleanFirstBufferMgr : AbstractBufferMgr
{
    public CleanFirstBufferMgr(FileMgr fm, LogMgr lm, int numbuffs) : base(fm, lm, numbuffs) { }

    protected override Buffer? ChooseUnpinnedBuffer()
    {
        // Prefer clean unpinned buffers
        foreach (Buffer buff in _bufferpool)
        {
            if (buff.IsPinned() == false && buff.ModifyingTxn() == -1)
                return buff;
        }
        // Fall back to any unpinned buffer
        foreach (Buffer buff in _bufferpool)
        {
            if (buff.IsPinned() == false)
                return buff;
        }
        return null;
    }
}

/// <summary>
/// LSN-based replacement strategy: among dirty pages, evicts the one with the lowest LSN.
/// Clean pages are preferred over dirty ones.
/// </summary>
public class LSNBasedBufferMgr : AbstractBufferMgr
{
    public LSNBasedBufferMgr(FileMgr fm, LogMgr lm, int numbuffs) : base(fm, lm, numbuffs) { }

    protected override Buffer? ChooseUnpinnedBuffer()
    {
        // Prefer clean unpinned buffers
        foreach (Buffer buff in _bufferpool)
        {
            if (buff.IsPinned() == false && buff.ModifyingTxn() == -1)
                return buff;
        }
        // Fall back to the dirty unpinned buffer with the lowest LSN
        int lowestLSN = int.MaxValue;
        Buffer? candidate = null;
        foreach (Buffer buff in _bufferpool)
        {
            if (buff.IsPinned() == false && buff.GetLSN() < lowestLSN)
            {
                lowestLSN = buff.GetLSN();
                candidate = buff;
            }
        }
        return candidate;
    }
}

/// <summary>
/// Naive replacement with O(1) block lookup via hash table.
/// </summary>
public class BufferMgrWithBufferHashTable : AbstractBufferMgr
{
    private Dictionary<BlockId, Buffer> _hashtable = new Dictionary<BlockId, Buffer>();

    public BufferMgrWithBufferHashTable(FileMgr fm, LogMgr lm, int numbuffs) : base(fm, lm, numbuffs) { }

    protected override Buffer? FindExistingBuffer(BlockId blk)
    {
        if (_hashtable.TryGetValue(blk, out Buffer? buff))
            return buff;
        return null;
    }

    protected override void OnBufferReplaced(Buffer buff, BlockId newBlk)
    {
        BlockId oldBlk = buff.Block();
        if (oldBlk != null)
            _hashtable.Remove(oldBlk);
        _hashtable.Add(newBlk, buff);
    }

    protected override Buffer? ChooseUnpinnedBuffer()
    {
        foreach (Buffer buff in _bufferpool)
        {
            if (buff.IsPinned() == false)
                return buff;
        }
        return null;
    }
}
