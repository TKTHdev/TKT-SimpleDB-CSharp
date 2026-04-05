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
    private static int nextTxNum = 0;
    private static int ENF_OF_FILE = -1;
    private RecoveryMgr recoveryMgr;
    private ConcurrencyMgr concurMgr;
    private BufferMgr bm;
    private FileMgr fm;
    private int txnum;
    private BufferList mybuffers;
    private static readonly object locker = new object();

    public Transaction(FileMgr fm, LogMgr lm, BufferMgr bm)
    {
        this.fm = fm;
        this.bm = bm;
        txnum = nextTxNumber();
        recoveryMgr = new RecoveryMgr(this, txnum, lm, bm);
        concurMgr = new ConcurrencyMgr();
        mybuffers = new BufferList(bm);
    }

    public void Commit()
    {
        recoveryMgr.commit();
        concurMgr.Release();
        mybuffers.UnpinAll();
        Console.WriteLine("transaction" + txnum + " committed");
    }

    public void Rollback()
    {
        recoveryMgr.rollback();
        concurMgr.Release();
        mybuffers.UnpinAll();
        Console.WriteLine("transaction" + txnum + " rolled back");
    }

    public void Recover()
    {
        bm.FlushAll(txnum);
        recoveryMgr.recover();
    }

    public void Pin(BlockId blk)
    {
        mybuffers.Pin(blk);
    }

    public void Unpin(BlockId blk)
    {
        mybuffers.Unpin(blk);
    }

    public int GetInt(BlockId blk, int offset)
    {
        concurMgr.SLock(blk);
        Buffer buff = mybuffers.GetBuffer(blk);
        return buff.Contents().GetInt(offset);
    }

    public string GetString(BlockId blk, int offset)
    {
        concurMgr.SLock(blk);
        Buffer buff = mybuffers.GetBuffer(blk);
        return buff.Contents().GetString(offset);
    }

    public void SetInt(BlockId blk, int offset, int val, bool okToLog)
    {
        concurMgr.XLock(blk);
        Buffer buff = mybuffers.GetBuffer(blk);
        int lsn = -1;
        if (okToLog)
            lsn = recoveryMgr.setInt(buff, offset, val);
        Page p = buff.Contents();
        p.SetInt(offset, val);
        buff.SetModified(txnum, lsn);
    }

    public void SetString(BlockId blk, int offset, string val, bool okToLog)
    {
        concurMgr.XLock(blk);
        Buffer buff = mybuffers.GetBuffer(blk);
        int lsn = -1;
        if (okToLog)
            lsn = recoveryMgr.setString(buff, offset, val);
        Page p = buff.Contents();
        p.SetString(offset, val);
        buff.SetModified(txnum, lsn);
    }
    public int Size(string filename)
    {
        BlockId dummyBlk = new BlockId(filename, ENF_OF_FILE);
        concurMgr.SLock(dummyBlk);
        return fm.Length(filename);
    }

    public BlockId Append(string filename)
    {
        BlockId dummyBlk = new BlockId(filename, ENF_OF_FILE);
        concurMgr.XLock(dummyBlk);
        return fm.Append(filename);
    }

    public int BlockSize()
    {
        return fm.BlockSize();
    }
    public int AvailableBuffs()
    {
        return bm.Available();
    }

    private static int nextTxNumber()
    {
        lock (locker)
        {
            nextTxNum++;
            Console.WriteLine("new transaction: " + nextTxNum);
            return nextTxNum; 
        }
    }
}