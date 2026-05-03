using DBSharp.Buffers;
using DBSharp.File;
using DBSharp.Log;
using DBSharp.Metadata;
using DBSharp.Planner;
using DBSharp.Transactions;

namespace DBSharp;

public class SimpleDB
{
    public const int DefaultBlockSize = 400;
    public const int DefaultBufferSize = 8;
    public const string LogFileName = "simpledb.log";

    private readonly FileMgr _fm;
    private readonly LogMgr _lm;
    private readonly IBufferMgr _bm;
    private readonly MetadataMgr _mdm;
    private readonly Planner.Planner _planner;

    public SimpleDB(string dirname)
        : this(dirname, DefaultBlockSize, DefaultBufferSize) { }

    public SimpleDB(string dirname, int blocksize, int buffsize)
    {
        _fm = new FileMgr(new DirectoryInfo(dirname), blocksize);
        _lm = new LogMgr(_fm, LogFileName);
        _bm = new BufferMgr(_fm, _lm, buffsize);

        Transaction tx = NewTx();
        bool isNew = _fm.IsNew();
        if (isNew)
            Console.WriteLine("creating new database");
        else
        {
            Console.WriteLine("recovering existing database");
            tx.Recover();
        }
        _mdm = new MetadataMgr(isNew, tx);
        var qp = new BasicQueryPlanner(_mdm);
        var up = new BasicUpdatePlanner(_mdm);
        _planner = new Planner.Planner(qp, up);
        tx.Commit();
    }

    public Transaction NewTx() => new Transaction(_fm, _lm, _bm);

    public MetadataMgr MdMgr() => _mdm;

    public Planner.Planner GetPlanner() => _planner;

    public FileMgr GetFileMgr() => _fm;

    public LogMgr GetLogMgr() => _lm;

    public IBufferMgr GetBufferMgr() => _bm;
}
