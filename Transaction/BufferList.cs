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
    private Dictionary<BlockId, Buffer> _buffers = new Dictionary<BlockId, Buffer>();
    private List<BlockId> _pins = new List<BlockId>();
    private BufferMgr _bm;

    public BufferList(BufferMgr bm)
    {
        _bm = bm;
    }

    public Buffer GetBuffer(BlockId blk)
    {
        return _buffers[blk];
    }

    public void Pin(BlockId blk)
    {
        Buffer buff = _bm.Pin(blk);
        _buffers[blk] = buff;
        _pins.Add(blk);
    }

    public void Unpin(BlockId blk)
    {
        Buffer buff = _buffers[blk];
        _bm.Unpin(buff);
        _pins.Remove(blk);
        if (!_pins.Contains(blk))
            _buffers.Remove(blk);
    }

    public void UnpinAll()
    {
        foreach (BlockId blk in _pins)
        {
            Buffer buff = _buffers[blk];
            _bm.Unpin(buff);
        }
        _buffers.Clear();
        _pins.Clear();
    }
}
