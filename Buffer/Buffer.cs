using DBSharp.File;
using DBSharp.Log;
using DBSharp.Buffer;

namespace DBSharp.Buffer;

public class Buffer
{
    private FileMgr _fm;
    private LogMgr _lm;
    private Page _contents;
    private BlockId _blk = null;
    private int _pins = 0;
    private int _txnum = -1;
    private int _lsn = -1;
    public Buffer(FileMgr fm, LogMgr lm)
    {
        _fm = fm;
        _lm = lm;
    }

    public Page Contents()
    {
        return _contents;
    }

    public BlockId Block()
    {
        return _blk;
    }

    public void SetModified(int txnum, int lsn)
    {
       _txnum = txnum;
       if(_lsn >= 0 ) _lsn = lsn;
    }

    public int ModifyingTxn()
    {
        return _txnum;
    }

    void AssignToBlock(BlockId b)
    {
        flush();
        _blk = b;
        _fm.Read(_blk, _contents);
        _pins = 0;
    }

    void flush()
    {
        if (_txnum >= 0)
        {
            _lm.Flush(_lsn);
            _fm.Write(_blk, _contents);
            _txnum = -1;
        }
    }

    void Pin()
    {
        _pins++;
    }

    void Unpin()
    {
        _pins--;
    }

    public bool IsPinned()
    {
        return _pins > 0;
    }
}