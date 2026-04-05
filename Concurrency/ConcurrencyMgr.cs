using DBSharp.File;

namespace DBSharp.Lock;

public class ConcurrencyMgr
{
    private static LockTable locktbl  = new LockTable();
    private Dictionary<BlockId, string> locks = new Dictionary<BlockId, string>();

    public void slock(BlockId blk)
    {
    }

    public void xlock(BlockId blk)
    {
        
    }

    public void release()
    {
        
    }

    private bool hasXLock(BlockId blk)
    {
        string locktype = locks[blk];
        return locktype != null && locktype.Equals("X");
    }
}