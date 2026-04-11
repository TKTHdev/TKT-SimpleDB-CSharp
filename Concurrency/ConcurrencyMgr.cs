using DBSharp.File;

namespace DBSharp.Concurrency;

/// <summary>
/// Per-transaction concurrency manager that tracks which locks the transaction holds
/// and delegates to the global <see cref="LockTable"/>. Ensures that an exclusive lock
/// is always preceded by a shared lock (lock upgrading).
/// </summary>
public class ConcurrencyMgr
{
    private static ILockTable _lockTable = new WaitDieLockTable();
    private Dictionary<BlockId, string> _locks = new Dictionary<BlockId, string>();
    private int _txNum;

    public ConcurrencyMgr(int txNum)
    {
        _txNum = txNum;
    }

    /// <summary>
    /// Resets the global lock table. Used to simulate a database restart in tests,
    /// where all in-flight transaction locks are lost.
    /// </summary>
    public static void ResetLockTable()
    {
        _lockTable = new WaitDieLockTable();
    }

    /// <summary>
    /// Resets the global lock table with the specified implementation.
    /// </summary>
    public static void ResetLockTable(ILockTable lockTable)
    {
        _lockTable = lockTable;
    }

    /// <summary>
    /// Acquires a shared lock on the specified block if one is not already held.
    /// </summary>
    /// <param name="blk">The block to lock.</param>
    public void SLock(BlockId blk)
    {
        if (!_locks.ContainsKey(blk))
        {
            _lockTable.SLock(blk, _txNum);
            _locks[blk] = "S";
        }
    }

    /// <summary>
    /// Acquires an exclusive lock on the specified block, upgrading from a shared lock if needed.
    /// </summary>
    /// <param name="blk">The block to lock exclusively.</param>
    public void XLock(BlockId blk)
    {
        if (!HasXLock(blk))
        {
            SLock(blk);
            _lockTable.XLock(blk, _txNum);
            _locks[blk] = "X";
        }
    }

    /// <summary>
    /// Releases all locks held by this transaction.
    /// </summary>
    public void Release()
    {
        foreach (BlockId blk in _locks.Keys)
            _lockTable.Unlock(blk, _txNum);
        _locks.Clear();
    }

    private bool HasXLock(BlockId blk)
    {
        return _locks.TryGetValue(blk, out string locktype) && locktype == "X";
    }
}
