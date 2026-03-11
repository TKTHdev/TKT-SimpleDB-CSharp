namespace DBSharp.File;
public class BlockId
{
    private string _filename;
    private int _blknum;
    public BlockId(string filename, int blknum)
    {
        _filename = filename;
        _blknum = blknum;
    }
    public string FileName()
    {
        return _filename;
    }
    public int Number()
    {
        return _blknum;
    }
    public override bool Equals(object? obj)
    {
        if (obj is BlockId blk)
            return _filename == blk._filename && _blknum == blk._blknum;
        return false;
    }
    public override string ToString()
    {
        return "[file" + _filename + ", block " + _blknum + "] ";
    }

    public override int GetHashCode()
    {
        return ToString().GetHashCode();
    }
}