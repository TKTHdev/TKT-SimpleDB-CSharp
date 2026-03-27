namespace DBSharp.Log;
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
                return null;
            default:
                return null;
        }
    }
}