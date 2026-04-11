using DBSharp.File;

namespace DBSharp.Concurrency;

/// <summary>
/// Global lock table that manages shared and exclusive locks on blocks
/// using the wound-wait deadlock prevention scheme.
/// Each transaction is identified by its transaction number (txnum);
/// a lower txnum means an older transaction.
/// When a conflict occurs:
///   - If the requester is older than the holder, it wounds the holder
///     (marks it for abort) and waits for the holder to release.
///   - If the requester is younger than the holder, it waits.
/// The wounded transaction discovers its fate on its next lock-table
/// operation (SLock, XLock, or Unlock) and throws <see cref="LockAbortException"/>.
/// This guarantees no deadlocks because younger transactions that block
/// older ones are always eventually aborted.
/// </summary>
public class WoundWaitLockTable : ILockTable
{
    private Dictionary<BlockId, HashSet<int>> _sLockHolders = new();
    private Dictionary<BlockId, int> _xLockHolder = new();
    private HashSet<int> _woundedTxns = new();

    /// <summary>
    /// Acquires a shared lock on the specified block using wound-wait.
    /// If an exclusive lock is held by a younger transaction, the requester
    /// wounds it and waits for it to release.
    /// If held by an older transaction, the requester waits.
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
                    _woundedTxns.Add(holder);
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
    /// If other shared lock holders are younger, the requester wounds them and waits.
    /// If any holder is older, the requester waits.
    /// </summary>
    public void XLock(BlockId blk, int txNum)
    {
        lock (this)
        {
            while (HasOtherSLocks(blk, txNum))
            {
                ThrowIfWounded(txNum);
                foreach (int holder in _sLockHolders[blk].Where(h => h != txNum))
                {
                    if (txNum < holder) // requester is older → wound
                        _woundedTxns.Add(holder);
                }
                Monitor.Wait(this);
            }
            ThrowIfWounded(txNum);
            _xLockHolder[blk] = txNum;
        }
    }

    /// <summary>
    /// Releases the lock held by the specified transaction on the specified block.
    /// If the transaction was wounded, throws <see cref="LockAbortException"/>
    /// without releasing the lock, so that the caller can roll back first.
    /// The subsequent Release during rollback will release the lock normally.
    /// </summary>
    public void Unlock(BlockId blk, int txNum)
    {
        lock (this)
        {
            if (_woundedTxns.Remove(txNum))
                throw new LockAbortException();
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

    private void ThrowIfWounded(int txNum)
    {
        if (_woundedTxns.Remove(txNum))
            throw new LockAbortException();
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
