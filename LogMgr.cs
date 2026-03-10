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
    public void Flush(int lsn)
    {
        if(lsn >= _lastSavedLSN)
            flush();
    }
    public IEnumerable<byte[]>GetEnumerator()
    {
       flush();
       return new LogIterator(_fm, _currentblk); 
    }
    public int Append(byte[] logrec)
    {
        int boundary = _logpage.GetInt(0);
        int recsize = logrec.Length;
        int bytesneeded = recsize + sizeof(int);
        if (boundary - bytesneeded < sizeof(int))
        {
            flush();
            _currentblk = appendNewBlock();
            boundary = _logpage.GetInt(0);
        }
        int recpos = boundary - bytesneeded;
        _logpage.SetBytes(recpos, logrec);
        _logpage.SetInt(0, recpos);
        _latestLSN += 1;
        return _latestLSN;
    }
    private BlockId appendNewBlock()
    {
        BlockId blk = _fm.Append(_logfile);
        _logpage.SetInt(0, _fm.BlockSize());
        _fm.Write(blk, _logpage);
        return blk;
    }
    private void flush()
    {
        _fm.Write(_currentblk, _logpage);
        _lastSavedLSN = _latestLSN;
    }
}