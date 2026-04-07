using DBSharp.File;

namespace DBSharp.Concurrency;

/// <summary>
/// Global lock table that manages shared and exclusive locks on blocks.
/// Lock values are stored as integers: positive values represent the number of
/// shared locks held, and -1 represents an exclusive lock.
/// The concurrency manager always acquires an SLock before requesting an XLock,
/// so a value greater than 1 indicates that other transactions also hold shared locks.
/// </summary>
public class LockTable
{
    private static readonly long MAX_TIME = 10000; // 10 seconds
    private Dictionary<BlockId, int> _locks = new Dictionary<BlockId, int>();

    /// <summary>
    /// Acquires a shared lock on the specified block. Waits if an exclusive lock is held,
    /// and throws <see cref="LockAbortException"/> if the wait times out.
    /// </summary>
    /// <param name="blk">The block to lock.</param>
    /// <exception cref="LockAbortException">Thrown if the lock cannot be acquired within the timeout.</exception>
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

    /// <summary>
    /// Acquires an exclusive lock on the specified block. Waits if other transactions
    /// hold shared locks, and throws <see cref="LockAbortException"/> if the wait times out.
    /// </summary>
    /// <param name="blk">The block to lock exclusively.</param>
    /// <exception cref="LockAbortException">Thrown if the lock cannot be acquired within the timeout.</exception>
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

    /// <summary>
    /// Releases one lock on the specified block. If this was the last shared lock (or an
    /// exclusive lock), the entry is removed and a waiting thread is notified.
    /// </summary>
    /// <param name="blk">The block to unlock.</param>
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
