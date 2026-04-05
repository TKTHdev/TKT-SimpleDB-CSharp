using DBSharp.File;

namespace DBSharp.Lock;
/*
 * Concurrency manager will always obtain an SLock on the block before requesting the Xlock
 * So a value higher than 1 indicates that some other transaction also has a lock on the block
 */

public class LockTable
{
    private static readonly long MAX_TIME = 10000; // 10 seconds
    private Dictionary<BlockId, int> _locks = new Dictionary<BlockId, int>();

    public void SLock(BlockId blk)
    {
        lock (this)
        {
            try
            {
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                while (HasXLock(blk) && !WaitingTooLong(timestamp))
                    Monitor.Wait(this, TimeSpan.FromMilliseconds(MAX_TIME));
                if (HasXLock(blk))
                    throw new LockAbortException();
                int val = GetLockVal(blk);
                _locks[blk] = val + 1;
            }
            catch
            {
                throw new LockAbortException();
            }
        }
    }
    public void XLock(BlockId blk)
    {
        lock (this)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            while (HasOtherSLocks(blk) && !WaitingTooLong(timestamp))
                Monitor.Wait(this, TimeSpan.FromMilliseconds(MAX_TIME));
            if (HasOtherSLocks(blk))
                throw new LockAbortException();
            _locks[blk] = -1;
        }
    }

    public void Unlock(BlockId blk)
    {
        lock (this)
        {
            int val = GetLockVal(blk);
            if (val > 1)
                _locks[blk] = val - 1;
            else
            {
                _locks.Remove(blk);
                Monitor.Pulse(this);
            }
        }
    }

    private bool HasXLock(BlockId blk)
    {
        return GetLockVal(blk) < 0;
    }
    private bool HasOtherSLocks(BlockId blk)
    {
        return GetLockVal(blk) > 1;
    }

    private bool WaitingTooLong(long startTime)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return now - startTime > MAX_TIME;
    }

    // Unlike Java's Map.get() which returns null for missing keys,
    // C# Dictionary[] throws KeyNotFoundException, so use TryGetValue.
    private int GetLockVal(BlockId blk)
    {
        return _locks.TryGetValue(blk, out int ival) ? ival : 0;
    }
}
