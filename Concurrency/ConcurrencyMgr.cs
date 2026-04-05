using DBSharp.File;

namespace DBSharp.Lock;

public class ConcurrencyMgr
{
    private static LockTable _lockTable = new LockTable();
    private Dictionary<BlockId, string> _locks = new Dictionary<BlockId, string>();

    public void SLock(BlockId blk)
    {
        if (!_locks.ContainsKey(blk))
        {
            _lockTable.SLock(blk);
            _locks[blk] = "S";
        }
    }

    public void XLock(BlockId blk)
    {
        if (!HasXLock(blk))
        {
            SLock(blk);
            _lockTable.XLock(blk);
            _locks[blk] = "X";
        }
    }

    public void Release()
    {
        foreach (BlockId blk in _locks.Keys)
            _lockTable.Unlock(blk);
        _locks.Clear();
    }

    private bool HasXLock(BlockId blk)
    {
        return _locks.TryGetValue(blk, out string locktype) && locktype == "X";
    }
}
