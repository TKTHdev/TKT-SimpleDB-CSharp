using DBSharp.File;
using DBSharp.Log;

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
        _contents = new Page(fm.BlockSize());
    }

    public Page Contents()
    {
        return _contents;
    }

    public BlockId Block()
    {
        return _blk;
    }

    public void SetModified(int txNum, int lsn)
    {
        _txnum = txNum;
        if (_lsn >= 0) _lsn = lsn;
    }

    public int ModifyingTxn()
    {
        return _txnum;
    }

    public void AssignToBlock(BlockId b)
    {
        Flush();
        _blk = b;
        _fm.Read(_blk, _contents);
        _pins = 0;
    }

    public void Flush()
    {
        if (_txnum >= 0)
        {
            _lm.Flush(_lsn);
            _fm.Write(_blk, _contents);
            _txnum = -1;
        }
    }

    public void Pin()
    {
        _pins++;
    }

    public void Unpin()
    {
        _pins--;
    }

    public bool IsPinned()
    {
        return _pins > 0;
    }

    public int GetLSN()
    {
        return _lsn;
    }
}