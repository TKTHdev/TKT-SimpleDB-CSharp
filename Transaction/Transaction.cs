using DBSharp.Buffers;
using DBSharp.File;
using DBSharp.Lock;
using DBSharp.Log;
namespace DBSharp.Transactions;


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
        recoveryMgr = new RecoveryMgr(this, txnum, lm, bm);
        concurMgr = new ConcurrencyMgr();
        mybuffers = new BufferList(bm);
    }

    public void Commit()
    {
        recoveryMgr.commit();
        concurMgr.Release();
        // mybuffers.unpinAll();
        Console.WriteLine("transaction" + txnum + " committed");
    }

    public void Rollback()
    {
        recoveryMgr.rollback();
        concurMgr.Release();
        // mybuffers.unpinAll();
        Console.WriteLine("transaction" + txnum + " rolled back");
    }

    public void Recovery()
    {

    }

    public void Pin(BlockId blk)
    {
    }
    public void Unpin(BlockId blk)
    { }

    public int GetInt(BlockId blk, int offset)
    {
        return 0;
    }

    public string GetString(BlockId blk, int offset)
    {
        return "";
    }

    public void SetInt(BlockId blk, int offset, int val, bool okToLog)
    {
    }

    public void SetString(BlockId blk, int offset, string val, bool okToLog)
    {

    }
    public int Size(string filename)
    {
        return 0;
    }

    public BlockId Append(string filename)
    {
        return null;
    }

    public int BlockSize()
    {
        return 0;
    }
    public int AvailableBuffs()
    {
        return 0;
    }

    private static int nextTxNumber()
    {
        lock (locker)
        {
            return 0;
        }
    }
}