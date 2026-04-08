namespace DBSharp.File;

/// <summary>
/// Manages read, write, and append operations on block-level database files.
/// Each file is treated as a sequence of fixed-size blocks.
/// </summary>
public class FileMgr
{
    /// <summary>
    /// Tracks I/O statistics (blocks written, read, and appended) for the file manager.
    /// </summary>
    public class FileMgrStatistics
    {
        private int _blocksWritten;
        private int _blocksRead;
        private int _blocksAppended;
        /// <summary>Creates a new statistics tracker with all counters at zero.</summary>
        public FileMgrStatistics()
        {
            _blocksRead = 0;
            _blocksWritten = 0;
            _blocksAppended = 0;
        }

        /// <summary>Records that blocks were written to disk.</summary>
        public void RecordBlocksWritten(int numofblocks) => _blocksWritten += numofblocks;

        /// <summary>Records that blocks were read from disk.</summary>
        public void RecordBlocksRead(int numofblocks) => _blocksRead += numofblocks;

        /// <summary>Records that blocks were appended to a file.</summary>
        public void RecordBlocksAppended(int numofblocks) => _blocksAppended += numofblocks;

        /// <summary>
        /// Returns the accumulated statistics as (written, read, appended).
        /// </summary>
        public (int, int, int) GetStats() => (_blocksWritten, _blocksRead, _blocksAppended);
    }
    private readonly DirectoryInfo _dbDirectory;
    private readonly int _blockSize;
    private readonly bool _isNew;
    private readonly Dictionary<string, FileStream> _openFiles = new();
    private FileMgrStatistics _fileMgrStats = new();

    /// <summary>
    /// Creates a new file manager for the given database directory.
    /// If the directory does not exist, it is created and marked as new.
    /// Any leftover temporary files (prefixed with "temp") are deleted on startup.
    /// </summary>
    /// <param name="dbDirectory">The directory where database files are stored.</param>
    /// <param name="blocksize">The fixed size of each block in bytes.</param>
    public FileMgr(DirectoryInfo dbDirectory, int blocksize)
    {
        _dbDirectory = dbDirectory;
        _blockSize = blocksize;
        _isNew = !dbDirectory.Exists;
        if (_isNew)
            _dbDirectory.Create();
        foreach (string filename in Directory.GetFiles(_dbDirectory.FullName))
            if (Path.GetFileName(filename).StartsWith("temp"))
                System.IO.File.Delete(filename);
    }

    /// <summary>
    /// Reads the contents of the specified block into the given page.
    /// </summary>
    /// <param name="blk">The block to read.</param>
    /// <param name="p">The page to populate with the block's data.</param>
    public void Read(BlockId blk, Page p)
    {
        lock (this)
        {
            try
            {
                FileStream fs = GetFile(blk.FileName());
                fs.Seek(blk.Number() * _blockSize, SeekOrigin.Begin);
                fs.Read(p.Contents(), 0, _blockSize);
                _fileMgrStats.RecordBlocksRead(1);
            }
            catch (IOException e)
            {
                throw new Exception("cannot read block " + blk, e);
            }
        }
    }

    /// <summary>
    /// Writes the contents of the given page to the specified block on disk.
    /// </summary>
    /// <param name="blk">The block to write to.</param>
    /// <param name="p">The page whose contents will be written.</param>
    public void Write(BlockId blk, Page p)
    {
        lock (this)
        {
            try
            {
                FileStream fs = GetFile(blk.FileName());
                fs.Seek(blk.Number() * _blockSize, SeekOrigin.Begin);
                fs.Write(p.Contents());
                _fileMgrStats.RecordBlocksWritten(1);
            }
            catch (IOException e)
            {
                throw new Exception("cannot write block " + blk, e);
            }
        }
    }

    /// <summary>
    /// Returns the number of blocks in the specified file.
    /// </summary>
    /// <param name="filename">The name of the file.</param>
    public int Length(string filename)
    {
        try
        {
            FileStream fs = GetFile(filename);
            return (int)(fs.Length / _blockSize);
        }
        catch (IOException e)
        {
            throw new Exception("cannot access " + filename, e);
        }
    }

    /// <summary>
    /// Appends a new zero-filled block to the end of the specified file
    /// and returns its <see cref="BlockId"/>.
    /// </summary>
    /// <param name="filename">The name of the file to extend.</param>
    public BlockId Append(string filename)
    {
        lock (this)
        {
            int newblknum = Length(filename);
            BlockId blk = new BlockId(filename, newblknum);
            byte[] b = new byte[_blockSize];
            try
            {
                FileStream fs = GetFile(blk.FileName());
                fs.Seek(blk.Number() * _blockSize, SeekOrigin.Begin);
                fs.Write(b);
                _fileMgrStats.RecordBlocksAppended(1);
            }
            catch (IOException e)
            {
                throw new Exception("cannot append block " + blk, e);
            }
            return blk;
        }
    }

    /// <summary>
    /// Removes the last block from the specified file.
    /// Used during recovery to undo an append operation.
    /// </summary>
    /// <param name="filename">The name of the file to truncate.</param>
    public void Truncate(string filename)
    {
        lock (this)
        {
            try
            {
                FileStream fs = GetFile(filename);
                long newLength = fs.Length - _blockSize;
                if (newLength < 0)
                    throw new IOException("file is already empty");
                fs.SetLength(newLength);
            }
            catch (IOException e)
            {
                throw new Exception("cannot truncate " + filename, e);
            }
        }
    }

    /// <summary>
    /// Returns whether the database directory was newly created by this file manager.
    /// </summary>
    public bool IsNew()
    {
        return _isNew;
    }

    /// <summary>
    /// Returns the fixed block size in bytes.
    /// </summary>
    public int BlockSize()
    {
        return _blockSize;
    }

    private FileStream GetFile(string filename)
    {
        if (_openFiles.TryGetValue(filename, out FileStream? fs))
            return fs;
        string path = Path.Combine(_dbDirectory.FullName, filename);
        fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        _openFiles[filename] = fs;
        return fs;
    }

    /// <summary>
    /// Returns the accumulated I/O statistics as (written, read, appended).
    /// </summary>
    public (int, int, int) GetStats()
    {
        return _fileMgrStats.GetStats();
    }
}
