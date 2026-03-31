namespace DBSharp.Log;
using DBSharp.File;

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
    void undo(int txnum);

    static LogRecord createLogRecord(byte[] bytes)
    {
        Page p = new Page(bytes);
        switch (p.GetInt(0))
        {
            case CHECKPOINT:
                return null;
            case START:
                return null;
            case COMMIT:
                return null;
            case ROLLBACK:
                return null;
            case SETINT:
                return null;
            case SETSTRING:
                return new SetStringRecord(p);
            default:
                return null;
        }
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
        // <SETSTRING(record type), 2(txnum), junk(filename), 44(blockid), 20(offset), hello(val1), ciao(val2)>
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
        return "<SETSTRING "+ txnum +" "+blk+" "+offset+" "+ val+ ">";
     }

    public void undo(int txnum)
    {
        
    }
}
