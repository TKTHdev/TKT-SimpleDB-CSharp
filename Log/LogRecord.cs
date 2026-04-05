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

    int Op();
    int TxNumber();
    void Undo(Transaction tx);

    static LogRecord CreateLogRecord(byte[] bytes)
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

    public int Op()
    {
        return LogRecord.CHECKPOINT;
    }

    public int TxNumber()
    {
        return -1;
    }

    public override string ToString()
    {
        return "<CHECKPOINT>";
    }

    public void Undo(Transaction tx) { }

    public static int WriteToLog(LogMgr lm)
    {
        byte[] rec = new byte[sizeof(int)];
        Page p = new Page(rec);
        p.SetInt(0, LogRecord.CHECKPOINT);
        return lm.Append(rec);
    }
}

public class StartRecord : LogRecord
{
    private int _txnum;

    public StartRecord(Page p)
    {
        int tpos = sizeof(int);
        _txnum = p.GetInt(tpos);
    }

    public int Op()
    {
        return LogRecord.START;
    }

    public int TxNumber()
    {
        return _txnum;
    }

    public override string ToString()
    {
        return "<START " + _txnum + ">";
    }

    public void Undo(Transaction tx) { }

    public static int WriteToLog(LogMgr lm, int txnum)
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
    private int _txnum;

    public CommitRecord(Page p)
    {
        int tpos = sizeof(int);
        _txnum = p.GetInt(tpos);
    }

    public int Op()
    {
        return LogRecord.COMMIT;
    }

    public int TxNumber()
    {
        return _txnum;
    }

    public override string ToString()
    {
        return "<COMMIT " + _txnum + ">";
    }

    public void Undo(Transaction tx) { }

    public static int WriteToLog(LogMgr lm, int txnum)
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
    private int _txnum;

    public RollbackRecord(Page p)
    {
        int tpos = sizeof(int);
        _txnum = p.GetInt(tpos);
    }

    public int Op()
    {
        return LogRecord.ROLLBACK;
    }

    public int TxNumber()
    {
        return _txnum;
    }

    public override string ToString()
    {
        return "<ROLLBACK " + _txnum + ">";
    }

    public void Undo(Transaction tx) { }

    public static int WriteToLog(LogMgr lm, int txnum)
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
    private int _txnum, _offset;
    private string _val;
    private BlockId _blk;

    public SetStringRecord(Page p)
    {
        // example of SET STRING RECORD:
        // <SETSTRING(record type), 2(txnum), junk(filename), 44(blockid), 20(offset), hello(old value), ciao(new value)>
        int tpos = sizeof(int);
        _txnum = p.GetInt(tpos);

        int fpos = tpos + sizeof(int);
        string filename = p.GetString(fpos);

        int bpos = fpos + Page.MaxLength(filename.Length);
        int blknum = p.GetInt(bpos);
        _blk = new BlockId(filename, blknum);

        int opos = bpos + sizeof(int);
        _offset = p.GetInt(opos);

        int vpos = opos + sizeof(int);
        _val = p.GetString(vpos);
    }

    public int Op()
    {
        return LogRecord.SETSTRING;
    }
    public int TxNumber()
    {
        return _txnum;
    }

    public override string ToString()
    {
        return "<SETSTRING " + _txnum + " " + _blk + " " + _offset + " " + _val + ">";
    }

    public void Undo(Transaction tx)
    {
        tx.Pin(_blk);
        tx.SetString(_blk, _offset, _val, false); // don't log the undo!
        tx.Unpin(_blk);
    }

    public static int WriteToLog(LogMgr lm, int txnum, BlockId blk, int offset, string val)
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

public class SetIntRecord : LogRecord
{
    private int _txnum, _offset;
    private int _val;
    private BlockId _blk;

    public SetIntRecord(Page p)
    {
        int tpos = sizeof(int);
        _txnum = p.GetInt(tpos);

        int fpos = tpos + sizeof(int);
        string filename = p.GetString(fpos);

        int bpos = fpos + Page.MaxLength(filename.Length);
        int blknum = p.GetInt(bpos);
        _blk = new BlockId(filename, blknum);

        int opos = bpos + sizeof(int);
        _offset = p.GetInt(opos);

        int vpos = opos + sizeof(int);
        _val = p.GetInt(vpos);
    }

    public int Op()
    {
        return LogRecord.SETINT;
    }

    public int TxNumber()
    {
        return _txnum;
    }

    public override string ToString()
    {
        return "<SETINT " + _txnum + " " + _blk + " " + _offset + " " + _val + ">";
    }

    public void Undo(Transaction tx)
    {
        tx.Pin(_blk);
        tx.SetInt(_blk, _offset, _val, false);
        tx.Unpin(_blk);
    }

    public static int WriteToLog(LogMgr lm, int txnum, BlockId blk, int offset, int val)
    {
        int tpos = sizeof(int);
        int fpos = tpos + sizeof(int);
        int bpos = fpos + Page.MaxLength(blk.FileName().Length);
        int opos = bpos + sizeof(int);
        int vpos = opos + sizeof(int);
        int reclen = vpos + sizeof(int);
        byte[] rec = new byte[reclen];
        Page p = new Page(rec);
        p.SetInt(0, LogRecord.SETINT);
        p.SetInt(tpos, txnum);
        p.SetString(fpos, blk.FileName());
        p.SetInt(bpos, blk.Number());
        p.SetInt(opos, offset);
        p.SetInt(vpos, val);
        return lm.Append(rec);
    }
}
