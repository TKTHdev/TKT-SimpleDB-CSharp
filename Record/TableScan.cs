using DBSharp.TmpInterface;
using DBSharp.Transactions;
using DBSharp.TmpClass;
using DBSharp.File;

namespace DBSharp.Record;

public class TableScan : UpdateScan
{
    private Transaction _tx;
    private Layout _layout;
    private RecordPage _rp;
    private string _filename;
    private int _currentSlot;

    public void Close()
    {
        if (_rp != null)
            _tx.Unpin(_rp.Block());
    }
}
