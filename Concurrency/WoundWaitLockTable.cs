using DBSharp.File;

namespace DBSharp.Concurrency;

/// <summary>
/// Global lock table that manages shared and exclusive locks on blocks
/// using the wound-wait deadlock prevention scheme.
/// Each transaction is identified by its transaction number (txnum);
/// a lower txnum means an older transaction.
/// When a conflict occurs:
///   - If the requester is older than the holder, it wounds the holder:
///     forcibly removes the holder's lock and marks it for abort.
///   - If the requester is younger than the holder, it waits.
/// This guarantees no deadlocks because older transactions never wait for younger ones
/// without first wounding them.
/// </summary>
public class WoundWaitLockTable : ILockTable
{
    private Dictionary<BlockId, HashSet<int>> _sLockHolders = new();
    private Dictionary<BlockId, int> _xLockHolder = new();
    private HashSet<int> _woundedTxns = new();

    /// <summary>
    /// Acquires a shared lock on the specified block using wound-wait.
    /// If an exclusive lock is held by a younger transaction, the requester wounds it
    /// (forcibly releases the lock and marks it for abort) and retries.
    /// If held by an older transaction, the requester waits (or aborts if itself wounded).
    /// </summary>
    public void SLock(BlockId blk, int txNum)
    {
        lock (this)
        {
            while (HasXLockByOther(blk, txNum))
            {
                ThrowIfWounded(txNum);
                int holder = _xLockHolder[blk];
                if (txNum < holder) // requester is older → wound the younger holder
                {
                    WoundTx(holder);
                    continue; // re-check — lock is now released
                }
                // requester is younger → wait
                Monitor.Wait(this);
            }
            ThrowIfWounded(txNum);
            if (!_sLockHolders.ContainsKey(blk))
                _sLockHolders[blk] = new HashSet<int>();
            _sLockHolders[blk].Add(txNum);
        }
    }

    /// <summary>
    /// Acquires an exclusive lock on the specified block using wound-wait.
    /// The caller must already hold an SLock (lock upgrading).
    /// If other shared lock holders are younger, the requester wounds them.
    /// If any holder is older, the requester waits (or aborts if itself wounded).
    /// </summary>
    public void XLock(BlockId blk, int txNum)
    {
        lock (this)
        {
            while (HasOtherSLocks(blk, txNum))
            {
                ThrowIfWounded(txNum);
                bool mustWait = false;
                foreach (int holder in _sLockHolders[blk].Where(h => h != txNum).ToList())
                {
                    if (txNum < holder) // requester is older → wound
                        WoundTx(holder);
                    else
                        mustWait = true;
                }
                if (mustWait)
                    Monitor.Wait(this);
            }
            ThrowIfWounded(txNum);
            _xLockHolder[blk] = txNum;
        }
    }

    /// <summary>
    /// Releases the lock held by the specified transaction on the specified block.
    /// Notifies all waiting threads so they can re-evaluate.
    /// Throws <see cref="LockAbortException"/> if this transaction was wounded.
    /// </summary>
    public void Unlock(BlockId blk, int txNum)
    {
        lock (this)
        {
            RemoveLocks(blk, txNum);
            bool wasWounded = _woundedTxns.Remove(txNum);
            Monitor.PulseAll(this);
            if (wasWounded)
                throw new LockAbortException();
        }
    }

    /// <summary>
    /// Wounds the specified transaction: forcibly removes all its locks across all blocks
    /// and marks it so that its next lock-table operation throws <see cref="LockAbortException"/>.
    /// </summary>
    private void WoundTx(int txNum)
    {
        _woundedTxns.Add(txNum);
        foreach (var blk in _xLockHolder.Where(kv => kv.Value == txNum).Select(kv => kv.Key).ToList())
            _xLockHolder.Remove(blk);
        foreach (var kv in _sLockHolders.ToList())
        {
            kv.Value.Remove(txNum);
            if (kv.Value.Count == 0)
                _sLockHolders.Remove(kv.Key);
        }
        Monitor.PulseAll(this);
    }

    private void ThrowIfWounded(int txNum)
    {
        if (_woundedTxns.Remove(txNum))
            throw new LockAbortException();
    }

    private void RemoveLocks(BlockId blk, int txNum)
    {
        if (_xLockHolder.TryGetValue(blk, out int holder) && holder == txNum)
            _xLockHolder.Remove(blk);
        if (_sLockHolders.TryGetValue(blk, out var holders))
        {
            holders.Remove(txNum);
            if (holders.Count == 0)
                _sLockHolders.Remove(blk);
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
}
