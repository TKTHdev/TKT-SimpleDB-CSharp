using DBSharp.File;
using DBSharp.Log;
using DBSharp.Buffer;

namespace DBSharp.Buffer;

public class Buffer
{
    public Buffer(FileMgr fm, LogMgr lm){}
    public Page contents(){return null;}
    public BlockId block(){return null;}
    public bool isPinned(){return false;}
    public void setModified(int txnum, int lsn){}
    public int modifyingTx(){return 0;}
}