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
}
