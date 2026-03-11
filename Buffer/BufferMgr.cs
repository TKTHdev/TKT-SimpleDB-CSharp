using DBSharp.File;
using DBSharp.Log;

namespace DBSharp.Buffer;

public class BufferMgr
{
    public BufferMgr(FileMgr fm, LogMgr lm, int numbuffs){}
    public Buffer pin(BlockId blk){return null;}
    public void unpin(BlockId buff){}
    public int available(){return 0;}
    public void flushAll(int txnum){}
}