using DBSharp.Buffers;
using DBSharp.File;
namespace DBSharp.Transactions;
using Buffer = DBSharp.Buffers.Buffer;

/// <summary>
/// Manages the list of currently pinned buffers for a single transaction.
/// Tracks which blocks are pinned and provides bulk unpin on transaction end.
/// </summary>
public class BufferList
{
    private Dictionary<BlockId, Buffer> _buffers = new Dictionary<BlockId, Buffer>();
    private List<BlockId> _pins = new List<BlockId>();
    private BufferMgr _bm;

    /// <summary>
    /// Creates a new buffer list backed by the given buffer manager.
    /// </summary>
    /// <param name="bm">The buffer manager used for pin/unpin operations.</param>
    public BufferList(BufferMgr bm)
    {
        _bm = bm;
    }

    /// <summary>
    /// Returns the buffer currently assigned to the specified block.
    /// </summary>
    /// <param name="blk">The block whose buffer to retrieve.</param>
    public Buffer GetBuffer(BlockId blk)
    {
        return _buffers[blk];
    }

    /// <summary>
    /// Pins the specified block via the buffer manager and records it in this list.
    /// </summary>
    /// <param name="blk">The block to pin.</param>
    public void Pin(BlockId blk)
    {
        Buffer buff = _bm.Pin(blk);
        _buffers[blk] = buff;
        _pins.Add(blk);
    }

    /// <summary>
    /// Unpins one reference to the specified block. If no more references remain,
    /// the block-to-buffer mapping is removed.
    /// </summary>
    /// <param name="blk">The block to unpin.</param>
    public void Unpin(BlockId blk)
    {
        Buffer buff = _buffers[blk];
        _bm.Unpin(buff);
        _pins.Remove(blk);
        if (!_pins.Contains(blk))
            _buffers.Remove(blk);
    }

    /// <summary>
    /// Unpins all buffers held by this transaction and clears the list.
    /// </summary>
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
