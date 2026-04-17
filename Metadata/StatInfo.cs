namespace DBSharp.Metadata;

public class StatInfo
{
    private int _numBlocks;
    private int _numRecs;

    public StatInfo(int numblocks, int numrecs)
    {
        _numBlocks = numblocks;
        _numRecs = numrecs;
    }

    public int BlocksAccessed()
    {
        return _numBlocks;
    }

    public int RecordsOutput()
    {
        return _numRecs;
    }

    public int DistinctValues(string fldname)
    {
        return 1 + (_numRecs / 3); // This is wildly inaccurate.
    }
}
