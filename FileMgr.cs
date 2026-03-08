namespace DBSharp;
public class FileMgr
{
    public class FileMgrStatistics
    {
        private int _blocksWritten;
        private int _blocksRead;
        private int _blocksAppended;
        public FileMgrStatistics()
        {
            _blocksRead = 0;
            _blocksWritten = 0;
            _blocksAppended = 0;
        }
        public void RecordBlocksWriten(int numofblocks) => _blocksWritten += numofblocks;
        public void RecordBlocksRead(int numofblocks) => _blocksRead += numofblocks;
        public void RecordBlocksAppended(int numofblocks) => _blocksAppended += numofblocks;
        public (int,int, int) GetStats() => (_blocksWritten, _blocksRead, _blocksAppended);
    }
    private readonly DirectoryInfo _dbDirectory;
    private readonly int _blockSize;
    private readonly bool _isNew;
    private readonly Dictionary<string, FileStream> _openFiles = new();
    private FileMgrStatistics FileMgrStats =  new();
    public FileMgr(DirectoryInfo dbDirectory, int blocksize)
    {
        _dbDirectory = dbDirectory;
        _blockSize = blocksize;
        _isNew = !dbDirectory.Exists;
        if (_isNew)
            _dbDirectory.Create();
        foreach (string filename in Directory.GetFiles(_dbDirectory.FullName))
            if (Path.GetFileName(filename).StartsWith("temp"))
                File.Delete(filename);
    }
    public void Read(BlockId blk, Page p)
    {
        lock (this)
        {
            try
            {
                FileStream fs = GetFile(blk.FileName());
                fs.Seek(blk.Number() * _blockSize, SeekOrigin.Begin);
                fs.Read(p.Contents(), 0, _blockSize);
                FileMgrStats.RecordBlocksRead(1);
            }
            catch (IOException e)
            {
                throw new Exception("cannot read block " + blk, e);
            }
        }
    }
    public void Write(BlockId blk, Page p)
    {
        lock (this)
        {
            try
            {
                FileStream fs = GetFile(blk.FileName());
                fs.Seek(blk.Number() * _blockSize, SeekOrigin.Begin);
                fs.Write(p.Contents());
                FileMgrStats.RecordBlocksWriten(1);
            }
            catch (IOException e)
            {
                throw new Exception("cannot write block " + blk, e);
            }
        }
    }
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
    public BlockId Append(string filename)
    {
        lock(this)
        {
            int newblknum = Length(filename);
            BlockId blk = new BlockId(filename, newblknum);
            byte[] b = new byte[_blockSize];
            try
            {
                FileStream fs = GetFile(blk.FileName());
                fs.Seek(blk.Number() * _blockSize, SeekOrigin.Begin);
                fs.Write(b);
            }
            catch(IOException e)
            {
                throw new Exception("cannot appene block " + blk, e);
            }
            return blk;
        }
    }
    public bool IsNew()
    {
        return _isNew;
    }
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
    public (int,int,int) GetStats()
    {
        return FileMgrStats.GetStats();
    }
}
