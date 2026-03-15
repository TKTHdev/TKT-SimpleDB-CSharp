using System.Collections;

using DBSharp.File;

namespace DBSharp.Log;

class LogIterator : IEnumerable<byte[]>, IEnumerator<byte[]>
{
    private FileMgr _fm;
    private BlockId _blk;
    private Page _p;
    private int _currentPos;
    private int _boundary;
    public LogIterator(FileMgr fm, BlockId blk)
    {
        _fm = fm;
        _blk = blk;
        byte[] b = new byte[fm.BlockSize()];
        _p = new Page(b);
        moveToBlock(_blk);
    }
    private void moveToBlock(BlockId blk)
    {
        // in spite of its name, 
        // it just loads the selected block to the page.
        // that's it.
        _fm.Read(blk, _p);
        _boundary = _p.GetInt(0);
        _currentPos = _boundary;
    }
    public byte[] Current { get; private set; } = Array.Empty<byte>();
    object IEnumerator.Current => Current;
    public bool MoveNext()
    {
        if (_currentPos >= _fm.BlockSize() && _blk.Number() <= 0)
            return false;
        if (_currentPos >= _fm.BlockSize())
        {
            _blk = new BlockId(_blk.FileName(), _blk.Number() - 1);
            moveToBlock(_blk);
        }
        // any record in the block is composed of:
        // [length of the record(int = 4 bytes)][record]
        Current = _p.GetBytes(_currentPos);
        _currentPos += sizeof(int) + Current.Length;
        return true;
    }
    public void Reset() { }
    public void Dispose() { }
    public IEnumerator<byte[]> GetEnumerator() => this;
    IEnumerator IEnumerable.GetEnumerator() => this;
}