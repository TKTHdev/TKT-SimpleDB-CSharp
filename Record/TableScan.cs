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

    public TableScan(Transaction tx, string tblname, Layout layout)
    {
        _tx = tx;
        _layout = layout;
        _filename = tblname + ".tbl";
        if (tx.Size(_filename) == 0)
            MoveToNewBlock();
        else
            MoveToBlock(0);
    }

    public void Close()
    {
        if (_rp != null)
            _tx.Unpin(_rp.Block());
    }

    public void BeforeFirst()
    {
        MoveToBlock(0);
    }

    public bool Next()
    {
        _currentSlot = _rp.NextAfter(_currentSlot);
        while (_currentSlot < 0)
        {
            if (AtLastBlock())
                return false;
            MoveToBlock(_rp.Block().Number() + 1);
            _currentSlot = _rp.NextAfter(_currentSlot);
        }
        return true;
    }

    public int GetInt(string fieldname)
    {
        return _rp.GetInt(_currentSlot, fieldname);
    }

    public string GetString(string fieldname)
    {
        return _rp.GetString(_currentSlot, fieldname);
    }

    public Constant GetVal(string fieldname)
    {
        if (_layout.GetSchema().Type(fieldname) == Schema.SqlType.INTEGER)
            return new Constant(GetInt(fieldname));
        else
            return new Constant(GetString(fieldname));
    }

    public bool HasField(string fieldname)
    {
        return _layout.GetSchema().HasField(fieldname);
    }

    public void SetInt(string fieldname, int val)
    {
        _rp.SetInt(_currentSlot, fieldname, val);
    }

    public void SetString(string fieldname, string val)
    {
        _rp.SetString(_currentSlot, fieldname, val);
    }

    public void SetVal(string fieldname, Constant val)
    {
        if (_layout.GetSchema().Type(fieldname) == Schema.SqlType.INTEGER)
            SetInt(fieldname, val.AsInt());
        else
            SetString(fieldname, val.AsString());
    }

    public void Insert()
    {
        _currentSlot = _rp.InsertAfter(_currentSlot);
        while (_currentSlot < 0)
        {
            if (AtLastBlock())
                MoveToNewBlock();
            else
                MoveToBlock(_rp.Block().Number() + 1);
            _currentSlot = _rp.InsertAfter(_currentSlot);
        }
    }

    public void Delete()
    {
        _rp.Delete(_currentSlot);
    }

    private void MoveToBlock(int blknum)
    {
        Close();
        BlockId blk = new BlockId(_filename, blknum);
        _rp = new RecordPage(_tx, blk, _layout);
        _currentSlot = -1;
    }

    private void MoveToNewBlock()
    {
        Close();
        BlockId blk = _tx.Append(_filename);
        _rp = new RecordPage(_tx, blk, _layout);
        _rp.Format();
        _currentSlot = -1;
    }

    private bool AtLastBlock()
    {
        return _rp.Block().Number() == _tx.Size(_filename) - 1;
    }
}
