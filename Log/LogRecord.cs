namespace DBSharp.Log;
using DBSharp.File;
using DBSharp.Transactions;

public interface LogRecord
{
    const int
        CHECKPOINT = 0,
        START = 1,
        COMMIT = 2,
        ROLLBACK = 3,
        SETINT = 4,
        SETSTRING = 5;

    int op();
    int txNumber();
    void undo(Transaction tx);

    static LogRecord createLogRecord(byte[] bytes)
    {
        Page p = new Page(bytes);
        switch (p.GetInt(0))
        {
            case CHECKPOINT:
                return new CheckpointRecord();
            case START:
                return new StartRecord(p);
            case COMMIT:
                return new CommitRecord(p);
            case ROLLBACK:
                return new RollbackRecord(p);
            case SETINT:
                return new SetIntRecord(p);
            case SETSTRING:
                return new SetStringRecord(p);
            default:
                return null;
        }
    }
}

public class CheckpointRecord : LogRecord
{
    public CheckpointRecord() { }

    public int op()
    {
        return LogRecord.CHECKPOINT;
    }

    public int txNumber()
    {
        return -1;
    }

    public string toString()
    {
        return "<CHECKPOINT>";
    }

    public void undo(Transaction tx) { }

    public static int writeToLog(LogMgr lm)
    {
        byte[] rec = new byte[sizeof(int)];
        Page p = new Page(rec);
        p.SetInt(0, LogRecord.CHECKPOINT);
        return lm.Append(rec);
    }
}

public class StartRecord : LogRecord
{
    private int txnum;

    public StartRecord(Page p)
    {
        int tpos = sizeof(int);
        txnum = p.GetInt(tpos);
    }

    public int op()
    {
        return LogRecord.START;
    }

    public int txNumber()
    {
        return txnum;
    }

    public string toString()
    {
        return "<START " + txnum + ">";
    }

    public void undo(Transaction tx) { }

    public static int writeToLog(LogMgr lm, int txnum)
    {
        byte[] rec = new byte[2 * sizeof(int)];
        Page p = new Page(rec);
        p.SetInt(0, LogRecord.START);
        p.SetInt(sizeof(int), txnum);
        return lm.Append(rec);
    }
}

public class CommitRecord : LogRecord
{
    private int txnum;

    public CommitRecord(Page p)
    {
        int tpos = sizeof(int);
        txnum = p.GetInt(tpos);
    }

    public int op()
    {
        return LogRecord.COMMIT;
    }

    public int txNumber()
    {
        return txnum;
    }

    public string toString()
    {
        return "<COMMIT " + txnum + ">";
    }

    public void undo(Transaction tx) { }

    public static int writeToLog(LogMgr lm, int txnum)
    {
        byte[] rec = new byte[2 * sizeof(int)];
        Page p = new Page(rec);
        p.SetInt(0, LogRecord.COMMIT);
        p.SetInt(sizeof(int), txnum);
        return lm.Append(rec);
    }
}

public class RollbackRecord : LogRecord
{
    private int txnum;

    public RollbackRecord(Page p)
    {
        int tpos = sizeof(int);
        txnum = p.GetInt(tpos);
    }

    public int op()
    {
        return LogRecord.ROLLBACK;
    }

    public int txNumber()
    {
        return txnum;
    }

    public string toString()
    {
        return "<ROLLBACK " + txnum + ">";
    }

    public void undo(Transaction tx) { }

    public static int writeToLog(LogMgr lm, int txnum)
    {
        byte[] rec = new byte[2 * sizeof(int)];
        Page p = new Page(rec);
        p.SetInt(0, LogRecord.ROLLBACK);
        p.SetInt(sizeof(int), txnum);
        return lm.Append(rec);
    }
}

public class SetStringRecord : LogRecord
{
    private int txnum, offset;
    private string val;
    private BlockId blk;

    public SetStringRecord(Page p)
    {
        // example of SET STRING RECORD:
        // <SETSTRING(record type), 2(txnum), junk(filename), 44(blockid), 20(offset), hello(old value), ciao(new value)>
        int tpos = sizeof(int);
        txnum = p.GetInt(tpos);

        int fpos = tpos + sizeof(int);
        string filename = p.GetString(fpos);

        int bpos = fpos + Page.MaxLength(filename.Length);
        int blknum = p.GetInt(bpos);
        blk = new BlockId(filename, blknum);

        int opos = bpos + sizeof(int);
        offset = p.GetInt(opos);

        int vpos = opos + sizeof(int);
        val = p.GetString(vpos);
    }

    public int op()
    {
        return LogRecord.SETSTRING;
    }
    public int txNumber()
    {
        return txnum;
    }

    public string toString()
    {
        return "<SETSTRING " + txnum + " " + blk + " " + offset + " " + val + ">";
    }

    public void undo(Transaction tx)
    {
        tx.Pin(blk);
        tx.SetString(blk, offset, val, false); // don't log the undo!
        tx.Unpin(blk);
    }

    public static int writeToLog(LogMgr lm, int txnum, BlockId blk, int offset, string val)
    {
        int tpos = sizeof(int);
        int fpos = tpos + sizeof(int);
        int bpos = fpos + Page.MaxLength(blk.FileName().Length);
        int opos = bpos + sizeof(int);
        int vpos = opos + sizeof(int);
        int reclen = vpos + Page.MaxLength(val.Length);
        byte[] rec = new byte[reclen];
        Page p = new Page(rec);
        p.SetInt(0, LogRecord.SETSTRING);
        p.SetInt(tpos, txnum);
        p.SetString(fpos, blk.FileName()); 
        p.SetInt(bpos, blk.Number());
        p.SetInt(opos, offset);
        p.SetString(vpos, val);
        return lm.Append(rec);
    }
}
