namespace DBSharp.Record;
using DBSharp.Transactions;
using DBSharp.File;

public class RecordPage
{
    public static readonly int EMPTY = 0, USED = 1;
    private Transaction _tx;
    private BlockId _blk;
    private Layout _layout;

    public RecordPage(Transaction tx, BlockId blk, Layout layout)
    {
        _tx = tx;
        _blk = blk;
        _layout = layout;
        tx.Pin(blk);
    }

    public int GetInt(int slot, string fieldname)
    {
        int fldpos = OffSet(slot) + _layout.GetOffset(fieldname);
        return _tx.GetInt(_blk, fldpos);
    }

    public string GetString(int slot, string fieldname)
    {
        int  fldpos = OffSet(slot) + _layout.GetOffset(fieldname);
        return _tx.GetString(_blk, fldpos);
    }
    public void SetInt(int slot, string fieldname, int value)
    {
        throw new NotImplementedException();
    }
    public void SetString(int slot, string fieldname, string value)
    {
        throw new NotImplementedException();
    }

    public void Delete(int slot)
    {
        throw new NotImplementedException();
    }

    public void Format()
    {
        throw new NotImplementedException();
    }
    public int NextAfter(int slot)
    {
        throw new NotImplementedException();
    }

    public int InsertAfter(int slot)
    {
        throw new NotImplementedException();
    }

    public BlockId Block()
    {
        return _blk;
    }

    private void SetFlag(int slot, int flag)
    {
        throw new NotImplementedException();
    }

    private int SearchAfter(int slot, int flag)
    {
        throw new NotImplementedException();
    }

    private bool IsValidSlot(int slot)
    {
        throw new NotImplementedException();
    }

    private int OffSet(int slot)
    {
        return slot * _layout.GetSlotSize();
    }
    
}