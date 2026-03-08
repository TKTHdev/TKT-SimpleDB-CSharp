using System.Runtime.Intrinsics.X86;

namespace DBSharp;

public class LogMgr
{
    private FileMgr _fm;
    private string _logfile;
    private Page _logpage; 
    private BlockId _currentblk;
    private int _latestLSN = 0;
    private int _lastSavedLSN = 0;
    
    public LogMgr(FileMgr fm, string logfile)
    {
        _fm = fm;
        _logfile = logfile;
        byte[]b = new byte[fm.BlockSize()];
        _logpage = new Page(b);
        int logsize = fm.Length(logfile);
        if (logsize == 0)
        {
            _currentblk = appendNewBlock();
        }
        else
        {
            _currentblk = new BlockId(logfile, logsize-1);
            fm.Read(_currentblk, _logpage);
        }
    }
    private BlockId appendNewBlock()
    {
        BlockId blk = _fm.Append(_logfile);
        _logpage.SetInt(0, _fm.BlockSize());
        _fm.Write(blk, _logpage);
        return blk;
    }
}