namespace DBSharp.File;

/// <summary>
/// Identifies a specific block within a file by its filename and block number.
/// </summary>
public class BlockId
{
    private string _filename;
    private int _blknum;

    /// <summary>
    /// Creates a new block identifier.
    /// </summary>
    /// <param name="filename">The name of the file containing the block.</param>
    /// <param name="blknum">The zero-based block number within the file.</param>
    public BlockId(string filename, int blknum)
    {
        _filename = filename;
        _blknum = blknum;
    }

    /// <summary>
    /// Returns the name of the file where the block resides.
    /// </summary>
    public string FileName()
    {
        return _filename;
    }

    /// <summary>
    /// Returns the zero-based block number within the file.
    /// </summary>
    public int Number()
    {
        return _blknum;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is BlockId blk)
            return _filename == blk._filename && _blknum == blk._blknum;
        return false;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return "[file" + _filename + ", block " + _blknum + "] ";
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return ToString().GetHashCode();
    }
}