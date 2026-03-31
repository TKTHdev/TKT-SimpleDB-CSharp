namespace DBSharp.Log;
using System.Transactions;
using DBSharp.File;

public interface LogRecord
{
    public const int
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
        
    }

    public int op()
    {
        return -1;
    }
    public int txNumber()
    {
        return txnum;
    }

    public void undo(int txnum)
    {
        
    }
}
