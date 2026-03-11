namespace DBSharp.Log;

using DBSharp.File;

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
        byte[] b = new byte[fm.BlockSize()];
        _logpage = new Page(b);
        // how many existing blocks we got in a file
        int logsize = fm.Length(logfile);
        if (logsize == 0)// when there's no existing block
        {
            _currentblk = appendNewBlock();
        }
        else
        {
            // when we have a existing block, then set currentblk = last block
            // then load that to the page
            _currentblk = new BlockId(logfile, logsize - 1);
            fm.Read(_currentblk, _logpage);
        }
    }
    public void Flush(int lsn)
    {
        // if there's any record that exists in a page but hasn't been written to disk
        // then flush()
        if (lsn >= _lastSavedLSN)
            flush();
        // the records whose lsn is higher than the one given might also be written to the disk
        /*
        log page:
        [boundary][enpty][LSN5[LSN4][LSN3]
        then Flush(lsn = 4j)
        */
    }
    public IEnumerable<byte[]> GetEnumerator()
    {
        flush();
        return new LogIterator(_fm, _currentblk);
    }
    // it just appends a new log record to the  page
    // ### it doesn't mean it is written to disk ###
    public int Append(byte[] logrec)
    {
        int boundary = _logpage.GetInt(0);
        int recsize = logrec.Length;
        int bytesneeded = recsize + sizeof(int);
        if (boundary - bytesneeded < sizeof(int))
        {
            // no more empty room for a new record so flush the current page 
            flush();
            // then new block 
            _currentblk = appendNewBlock();
            // reset page
            boundary = _logpage.GetInt(0);
        }
        int recpos = boundary - bytesneeded;
        _logpage.SetBytes(recpos, logrec);
        _logpage.SetInt(0, recpos); //update boundary
        // update lsn
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
        // just write the whole page to disk
        _fm.Write(_currentblk, _logpage);
        _lastSavedLSN = _latestLSN;
    }
}