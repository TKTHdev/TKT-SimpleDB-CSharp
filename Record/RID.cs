namespace DBSharp.Record;

public class RID
{
    private readonly int _blknum;
    private readonly int _slot;

    public RID(int blknum, int slot)
    {
        _blknum = blknum;
        _slot = slot;
    }

    public int BlockNumber()
    {
        return _blknum;
    }

    public int Slot()
    {
        return _slot;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not RID r) return false;
        return _blknum == r._blknum && _slot == r._slot;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_blknum, _slot);
    }

    public override string ToString()
    {
        return $"[{_blknum}, {_slot}]";
    }
}
