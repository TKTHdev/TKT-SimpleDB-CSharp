using System.Collections;

using DBSharp.File;

namespace DBSharp.Log;

/// <summary>
/// Iterates over log records in chronological order, starting from the
/// earliest block (block 0) and moving forward through later blocks.
/// Within each block, records are yielded from oldest to newest.
/// Each record is returned as a raw byte array.
/// </summary>
class ForwardLogIterator : IEnumerable<byte[]>, IEnumerator<byte[]>
{
    private FileMgr _fm;
    private BlockId _lastblk;
    private Page _p;
    private int _currentBlkNum;
    // positions of records within the current block, ordered oldest-first
    private List<int> _positions = new List<int>();
    private int _posIndex;

    /// <summary>
    /// Creates a new forward log iterator starting from block 0 up to the given block.
    /// </summary>
    /// <param name="fm">The file manager for reading blocks.</param>
    /// <param name="lastblk">The last block to iterate through (typically the current log block).</param>
    public ForwardLogIterator(FileMgr fm, BlockId lastblk)
    {
        _fm = fm;
        _lastblk = lastblk;
        byte[] b = new byte[fm.BlockSize()];
        _p = new Page(b);
        _currentBlkNum = 0;
        MoveToBlock(_currentBlkNum);
    }

    private void MoveToBlock(int blknum)
    {
        BlockId blk = new BlockId(_lastblk.FileName(), blknum);
        _fm.Read(blk, _p);
        int boundary = _p.GetInt(0);
        // Collect all record positions within this block.
        // Records are stored from boundary toward the end of the block.
        // Each record: [length(int)][record bytes]
        _positions.Clear();
        int pos = boundary;
        while (pos < _fm.BlockSize())
        {
            _positions.Add(pos);
            byte[] rec = _p.GetBytes(pos);
            pos += sizeof(int) + rec.Length;
        }
        _posIndex = 0;
    }

    /// <inheritdoc/>
    public byte[] Current { get; private set; } = Array.Empty<byte>();
    object IEnumerator.Current => Current;

    /// <inheritdoc/>
    public bool MoveNext()
    {
        // if we've exhausted records in the current block, move to the next block
        while (_posIndex >= _positions.Count)
        {
            if (_currentBlkNum >= _lastblk.Number())
                return false;
            _currentBlkNum++;
            MoveToBlock(_currentBlkNum);
        }
        Current = _p.GetBytes(_positions[_posIndex]);
        _posIndex++;
        return true;
    }

    /// <inheritdoc/>
    public void Reset() { }

    /// <inheritdoc/>
    public void Dispose() { }

    /// <inheritdoc/>
    public IEnumerator<byte[]> GetEnumerator() => this;
    IEnumerator IEnumerable.GetEnumerator() => this;
}
