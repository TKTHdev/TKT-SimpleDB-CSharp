using System.Runtime.InteropServices.JavaScript;
using DBSharp.Buffers;
using DBSharp.File;
namespace DBSharp.Transactions;
using Buffer = DBSharp.Buffers.Buffer;

/*
 * the class BufferList manages the list of
 * currently pinned buffers for a transaction
 */
public class BufferList
{
    private Dictionary<BlockId, Buffer> buffers = new Dictionary<BlockId, Buffer>();
    private List<BlockId> pins = new List<BlockId>();
    private BufferMgr bm;

    public BufferList(BufferMgr bm)
    {
    }

    public Buffer GetBuffer(BlockId blk)
    {
        return buffers[blk];
    }

    public void Pin(BlockId blk)
    {
    }

    public void Unpin(BlockId blk)
    {
    }

    public void UnpinAll()
    {
    }
    
}