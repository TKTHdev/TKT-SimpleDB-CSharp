namespace DBSharp;
public class FileMgr
{
    private readonly DirectoryInfo _dbDirectory;
    private readonly int _blockSize;
    private readonly bool _isNew;
    private readonly Dictionary<string, FileStream> _openFiles = new();
    public FileMgr(DirectoryInfo dbDirectory, int blocksize)
    {
        _dbDirectory = dbDirectory;
        _blockSize = blocksize;
        _isNew = dbDirectory.Exists;
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
}