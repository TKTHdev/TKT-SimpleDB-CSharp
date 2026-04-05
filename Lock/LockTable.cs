using DBSharp.File;

namespace DBSharp.Lock;

public class LockTable
{
   private static readonly long MAX_TIME = 10000; // 10 seconds
   private Dictionary<BlockId, int>locks =  new Dictionary<BlockId, int>();

   public void SLock(BlockId blk)
   {
      lock(this)
      {}
   }
   public void XLock(BlockId blk)
   {
      lock(this)
      {}
   }

   public void Unlock(BlockId blk)
   {
      lock (this)
      {
         
      }
   }

   private bool hasXLock(BlockId blk)
   {
      return true;
   }
   private bool hasOtherSLocks(BlockId blk)
   {
      return true;
   }

   private bool waitingTooLong(long startTime)
   {
      return true;
   }

   private int getLockVal(BlockId blk)
   {
      return 0;
   }
}