using DBSharp.File;

namespace DBSharp.Concurrency;

public interface ILockTable
{
    void SLock(BlockId blk, int txNum);
    void XLock(BlockId blk, int txNum);
    void Unlock(BlockId blk, int txNum);
}
