using System.Collections;

using DBSharp.File;

namespace DBSharp.Log;

/// <summary>
/// Iterates over log records in reverse chronological order, starting from the
/// most recent block and moving backward through earlier blocks. Each record is
/// returned as a raw byte array.
/// </summary>
class BackwardLogIterator : IEnumerable<byte[]>, IEnumerator<byte[]>
{
    private FileMgr _fm;
    private BlockId _blk;
    private Page _p;
    private int _currentPos;
    private int _boundary;

    /// <summary>
    /// Creates a new log iterator starting from the given block.
    /// </summary>
    /// <param name="fm">The file manager for reading blocks.</param>
    /// <param name="blk">The block to start iterating from (typically the current log block).</param>
    public BackwardLogIterator(FileMgr fm, BlockId blk)
    {
        _fm = fm;
        _blk = blk;
        byte[] b = new byte[fm.BlockSize()];
        _p = new Page(b);
        MoveToBlock(_blk);
    }
    private void MoveToBlock(BlockId blk)
    {
        // in spite of its name,
        // it just loads the selected block to the page.
        // that's it.
        _fm.Read(blk, _p);
        _boundary = _p.GetInt(0);
        _currentPos = _boundary;
    }

    /// <inheritdoc/>
    public byte[] Current { get; private set; } = Array.Empty<byte>();
    object IEnumerator.Current => Current;

    /// <inheritdoc/>
    public bool MoveNext()
    {
        if (_currentPos >= _fm.BlockSize() && _blk.Number() <= 0)
            return false;
        if (_currentPos >= _fm.BlockSize())
        {
            _blk = new BlockId(_blk.FileName(), _blk.Number() - 1);
            MoveToBlock(_blk);
        }
        // any record in the block is composed of:
        // [length of the record(int = 4 bytes)][record]
        Current = _p.GetBytes(_currentPos);
        _currentPos += sizeof(int) + Current.Length;
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