using DBSharp.File;

namespace DBSharp.Lock;

public class LockTable
{
   private static readonly long MAX_TIME = 10000; // 10 seconds
   private Dictionary<BlockId, int>locks =  new Dictionary<BlockId, int>();

   public void SLock(BlockId blk)
   {
      lock (this)
      {
         try
         {
           long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            while(hasXLock(blk) && !waitingTooLong(timestamp))
               Monitor.Wait(this, TimeSpan.FromMilliseconds(MAX_TIME));
            if (hasOtherSLocks(blk))
               throw new LockAbortException();
            int val = getLockVal(blk); 
            locks[blk] = val + 1;
         }
         catch
         {
            throw new LockAbortException();
         }
      }
   }
   public void XLock(BlockId blk)
   {
      lock (this)
      {
         long  timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
         while(hasOtherSLocks(blk) && !waitingTooLong(timestamp))
            Monitor.Wait(this, TimeSpan.FromMilliseconds(MAX_TIME));
         if (hasXLock(blk))
            throw new LockAbortException();
         locks[blk] = -1;
      }
   }

   public void Unlock(BlockId blk)
   {
      lock (this)
      {
         int val = getLockVal(blk);
         if (val > 1)
            locks[blk] = val - 1;
         else
         {
            locks.Remove(blk);
            Monitor.Pulse(this);
         }
      }
   }

   private bool hasXLock(BlockId blk)
   {
      return getLockVal(blk) < 0;
   }
   private bool hasOtherSLocks(BlockId blk)
   {
      return getLockVal(blk) > 1;
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