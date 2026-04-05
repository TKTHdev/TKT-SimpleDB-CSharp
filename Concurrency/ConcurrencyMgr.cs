using DBSharp.File;

namespace DBSharp.Lock;

public class ConcurrencyMgr
{
    private static LockTable locktbl  = new LockTable();
    private Dictionary<BlockId, string> locks = new Dictionary<BlockId, string>();

    public void SLock(BlockId blk)
    {
        if (!locks.ContainsKey(blk))
        {
            locktbl.SLock(blk);
            locks[blk] = "S";
        }
    }

    public void XLock(BlockId blk)
    {
        if (!hasXLock(blk))
        {
            SLock(blk);
            locktbl.XLock(blk);
            locks[blk] = "X";
        }
    }

    public void Release()
    {
        foreach (BlockId blk in locks.Keys)
            locktbl.Unlock(blk);
        locks.Clear();
    }

    private bool hasXLock(BlockId blk)
    {
        string locktype = locks[blk];
        return locktype != null && locktype.Equals("X");
    }
}