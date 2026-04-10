using DBSharp.File;

namespace DBSharp.Concurrency;

/// <summary>
/// Global lock table that manages shared and exclusive locks on blocks
/// using the wait-die deadlock prevention scheme.
/// Each transaction is identified by its transaction number (txnum);
/// a lower txnum means an older transaction.
/// When a conflict occurs:
///   - If the requester is older than the holder, it waits.
///   - If the requester is younger than the holder, it dies (throws <see cref="LockAbortException"/>).
/// This guarantees no deadlocks because younger transactions never wait for older ones.
/// </summary>
public class LockTable
{
    private Dictionary<BlockId, HashSet<int>> _sLockHolders = new();
    private Dictionary<BlockId, int> _xLockHolder = new();

    /// <summary>
    /// Acquires a shared lock on the specified block using wait-die.
    /// If an exclusive lock is held by a younger transaction, the requester waits.
    /// If held by an older transaction, the requester dies.
    /// </summary>
    /// <param name="blk">The block to lock.</param>
    /// <param name="txNum">The transaction number of the requester.</param>
    /// <exception cref="LockAbortException">Thrown if the requester is younger than the holder (die).</exception>
    public void SLock(BlockId blk, int txNum)
    {
        lock (this)
        {
            while (HasXLockByOther(blk, txNum))
            {
                int holder = _xLockHolder[blk];
                if (txNum < holder) // requester is older → wait
                    Monitor.Wait(this);
                else // requester is younger → die
                    throw new LockAbortException();
            }
            if (!_sLockHolders.ContainsKey(blk))
                _sLockHolders[blk] = new HashSet<int>();
            _sLockHolders[blk].Add(txNum);
        }
    }

    /// <summary>
    /// Acquires an exclusive lock on the specified block using wait-die.
    /// The caller must already hold an SLock (lock upgrading).
    /// If all other shared lock holders are younger, the requester waits.
    /// If any holder is older, the requester dies.
    /// </summary>
    /// <param name="blk">The block to lock exclusively.</param>
    /// <param name="txNum">The transaction number of the requester.</param>
    /// <exception cref="LockAbortException">Thrown if any holder is older than the requester (die).</exception>
    public void XLock(BlockId blk, int txNum)
    {
        lock (this)
        {
            while (HasOtherSLocks(blk, txNum))
            {
                int oldestOther = OldestOtherSLockHolder(blk, txNum);
                if (txNum < oldestOther) // requester is older than all others → wait
                    Monitor.Wait(this);
                else // requester is younger than some holder → die
                    throw new LockAbortException();
            }
            _xLockHolder[blk] = txNum;
        }
    }

    /// <summary>
    /// Releases the lock held by the specified transaction on the specified block.
    /// Notifies all waiting threads so they can re-evaluate the wait-die condition.
    /// </summary>
    /// <param name="blk">The block to unlock.</param>
    /// <param name="txNum">The transaction number releasing the lock.</param>
    public void Unlock(BlockId blk, int txNum)
    {
        lock (this)
        {
            if (_xLockHolder.TryGetValue(blk, out int holder) && holder == txNum)
                _xLockHolder.Remove(blk);
            if (_sLockHolders.TryGetValue(blk, out var holders))
            {
                holders.Remove(txNum);
                if (holders.Count == 0)
                    _sLockHolders.Remove(blk);
            }
            Monitor.PulseAll(this);
        }
    }

    private bool HasXLockByOther(BlockId blk, int txNum)
    {
        return _xLockHolder.TryGetValue(blk, out int holder) && holder != txNum;
    }

    private bool HasOtherSLocks(BlockId blk, int txNum)
    {
        if (!_sLockHolders.TryGetValue(blk, out var holders))
            return false;
        return holders.Any(h => h != txNum);
    }

    private int OldestOtherSLockHolder(BlockId blk, int txNum)
    {
        return _sLockHolders[blk].Where(h => h != txNum).Min();
    }
}
