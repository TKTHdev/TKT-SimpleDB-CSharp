namespace DBSharp.Log;

using DBSharp.File;

/// <summary>
/// Manages the write-ahead log (WAL). Log records are appended to an in-memory page
/// and flushed to disk when the page is full or when an explicit flush is requested.
/// Records are written from the end of the page toward the front; the first integer
/// in the page stores the boundary (offset of the most recently written record).
/// </summary>
public class LogMgr
{
    private FileMgr _fm;
    private string _logfile;
    private Page _logpage;
    private BlockId _currentblk;
    private int _latestLSN = 0;
    private int _lastSavedLSN = 0;

    /// <summary>
    /// Creates a new log manager. If the log file is empty, an initial block is appended;
    /// otherwise the last existing block is loaded into memory.
    /// </summary>
    /// <param name="fm">The file manager used for disk I/O.</param>
    /// <param name="logfile">The name of the log file.</param>
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
            _currentblk = AppendNewBlock();
        }
        else
        {
            // when we have a existing block, then set currentblk = last block
            // then load that to the page
            _currentblk = new BlockId(logfile, logsize - 1);
            fm.Read(_currentblk, _logpage);
        }
    }

    /// <summary>
    /// Ensures that the log record with the given LSN (and all earlier records on the
    /// same page) has been written to disk.
    /// </summary>
    /// <param name="lsn">The log sequence number to flush up to.</param>
    public void Flush(int lsn)
    {
        // if there's any record that exists in a page but hasn't been written to disk
        // then Flush()
        if (lsn >= _lastSavedLSN)
            Flush();
        // the records whose lsn is higher than the one given might also be written to the disk
        /*
        log page:
        [boundary][enpty][LSN5[LSN4][LSN3]
        then Flush(lsn = 4j)
        */
    }

    /// <summary>
    /// Flushes the current log page and returns an iterator over all log records
    /// in reverse order (most recent first).
    /// </summary>
    public IEnumerable<byte[]> GetEnumerator()
    {
        Flush();
        return new LogIterator(_fm, _currentblk);
    }

    /// <summary>
    /// Appends a new log record to the in-memory log page. If the page is full,
    /// it is flushed to disk and a new block is allocated. Returns the LSN assigned
    /// to the new record. Note: the record is not guaranteed to be on disk until
    /// <see cref="Flush(int)"/> is called.
    /// </summary>
    /// <param name="logrec">The raw bytes of the log record.</param>
    /// <returns>The log sequence number (LSN) assigned to this record.</returns>
    public int Append(byte[] logrec)
    {
        int boundary = _logpage.GetInt(0);
        int recsize = logrec.Length;
        int bytesneeded = recsize + sizeof(int);
        if (boundary - bytesneeded < sizeof(int))
        {
            // no more empty room for a new record so flush the current page
            Flush();
            // then new block
            _currentblk = AppendNewBlock();
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
    private BlockId AppendNewBlock()
    {
        BlockId blk = _fm.Append(_logfile);
        _logpage.SetInt(0, _fm.BlockSize());
        _fm.Write(blk, _logpage);
        return blk;
    }
    private void Flush()
    {
        // just write the whole page to disk
        _fm.Write(_currentblk, _logpage);
        _lastSavedLSN = _latestLSN;
    }
}