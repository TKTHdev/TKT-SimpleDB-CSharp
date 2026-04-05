using DBSharp.Buffers;
using DBSharp.File;
using DBSharp.Lock;
using DBSharp.Log;
namespace DBSharp.Transactions;
using Buffer = DBSharp.Buffers.Buffer;


/*
 *
 */

public class Transaction
{
    private static int _nextTxNum = 0;
    private static int END_OF_FILE = -1;
    private RecoveryMgr _recoveryMgr;
    private ConcurrencyMgr _concurMgr;
    private BufferMgr _bm;
    private FileMgr _fm;
    private int _txnum;
    private BufferList _myBuffers;
    private static readonly object _locker = new object();

    public Transaction(FileMgr fm, LogMgr lm, BufferMgr bm)
    {
        _fm = fm;
        _bm = bm;
        _txnum = NextTxNumber();
        _recoveryMgr = new RecoveryMgr(this, _txnum, lm, bm);
        _concurMgr = new ConcurrencyMgr();
        _myBuffers = new BufferList(bm);
    }

    public void Commit()
    {
        _recoveryMgr.Commit();
        _concurMgr.Release();
        _myBuffers.UnpinAll();
        Console.WriteLine("transaction" + _txnum + " committed");
    }

    public void Rollback()
    {
        _recoveryMgr.Rollback();
        _concurMgr.Release();
        _myBuffers.UnpinAll();
        Console.WriteLine("transaction" + _txnum + " rolled back");
    }

    public void Recover()
    {
        _bm.FlushAll(_txnum);
        _recoveryMgr.Recover();
    }

    public void Pin(BlockId blk)
    {
        _myBuffers.Pin(blk);
    }

    public void Unpin(BlockId blk)
    {
        _myBuffers.Unpin(blk);
    }

    public int GetInt(BlockId blk, int offset)
    {
        _concurMgr.SLock(blk);
        Buffer buff = _myBuffers.GetBuffer(blk);
        return buff.Contents().GetInt(offset);
    }

    public string GetString(BlockId blk, int offset)
    {
        _concurMgr.SLock(blk);
        Buffer buff = _myBuffers.GetBuffer(blk);
        return buff.Contents().GetString(offset);
    }

    public void SetInt(BlockId blk, int offset, int val, bool okToLog)
    {
        _concurMgr.XLock(blk);
        Buffer buff = _myBuffers.GetBuffer(blk);
        int lsn = -1;
        if (okToLog)
            lsn = _recoveryMgr.SetInt(buff, offset, val);
        Page p = buff.Contents();
        p.SetInt(offset, val);
        buff.SetModified(_txnum, lsn);
    }

    public void SetString(BlockId blk, int offset, string val, bool okToLog)
    {
        _concurMgr.XLock(blk);
        Buffer buff = _myBuffers.GetBuffer(blk);
        int lsn = -1;
        if (okToLog)
            lsn = _recoveryMgr.SetString(buff, offset, val);
        Page p = buff.Contents();
        p.SetString(offset, val);
        buff.SetModified(_txnum, lsn);
    }
    public int Size(string filename)
    {
        BlockId dummyBlk = new BlockId(filename, END_OF_FILE);
        _concurMgr.SLock(dummyBlk);
        return _fm.Length(filename);
    }

    public BlockId Append(string filename)
    {
        BlockId dummyBlk = new BlockId(filename, END_OF_FILE);
        _concurMgr.XLock(dummyBlk);
        return _fm.Append(filename);
    }

    public int BlockSize()
    {
        return _fm.BlockSize();
    }
    public int AvailableBuffs()
    {
        return _bm.Available();
    }

    private static int NextTxNumber()
    {
        lock (_locker)
        {
            _nextTxNum++;
            Console.WriteLine("new transaction: " + _nextTxNum);
            return _nextTxNum;
        }
    }
}
