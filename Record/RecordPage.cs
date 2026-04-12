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
        int fldpos  = OffSet(slot) + _layout.GetOffset(fieldname);
        _tx.SetInt(_blk, fldpos, value, true);
    }
    public void SetString(int slot, string fieldname, string value)
    {
        int fldpos   = OffSet(slot) + _layout.GetOffset(fieldname);
        _tx.SetString(_blk, fldpos, value, true);
    }

    public void Delete(int slot)
    {
        SetFlag(slot, EMPTY);
    }

    public void Format()
    {
        int slot = 0;
        while (IsValidSlot(slot))
        {
            _tx.SetInt(_blk, OffSet(slot), EMPTY, false);
            Schema sch = _layout.GetSchema();
            foreach (string fieldname in sch.Fields())
            {
                int fldpos = OffSet(slot) + _layout.GetOffset(fieldname);
                if(sch.Type(fieldname)==Schema.SqlType.INTEGER) 
                    _tx.SetInt(_blk, fldpos, 0, false);
                else // when it is a string field
                    _tx.SetString(_blk, fldpos, "", false);        
            }
            slot++;
        }
    }
    public int NextAfter(int slot)
    {
        return SearchAfter(slot, USED);
    }

    public int InsertAfter(int slot)
    {
        int newslot = SearchAfter(slot, EMPTY);
        if (newslot >= 0)
            SetFlag(newslot, USED);
        return newslot;
    }

    public BlockId Block()
    {
        return _blk;
    }

    private void SetFlag(int slot, int flag)
    {
        _tx.SetInt(_blk, OffSet(slot),flag, true);
    }

    private int SearchAfter(int slot, int flag)
    {
        slot++;
        while (IsValidSlot(slot))
        {
            if (_tx.GetInt(_blk, OffSet(slot)) == flag)
                return slot;
            slot++;
        }

        return -1;
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