using DBSharp.Buffer;
using DBSharp.File;
using DBSharp.Log;
using System.Text;

var tests = new (string Name, Action Body)[]
{
    ("BlockId exposes constructor values", BlockId_ExposesConstructorValues),
    ("BlockId equality and hash code use file and block number", BlockId_EqualityAndHashCode),
    ("BlockId ToString keeps the current format", BlockId_ToStringFormat),
    ("Page stores and reads integers", Page_IntRoundTrip),
    ("Page stores and reads byte arrays without aliasing", Page_ByteRoundTripAndIsolation),
    ("Page stores and reads strings", Page_StringRoundTrip),
    ("Page MaxLength follows the declared charset", Page_MaxLengthMatchesCharsetContract),
    ("FileMgr creates missing directories and reports new databases", FileMgr_CreatesMissingDirectory),
    ("FileMgr reports existing directories as not new", FileMgr_ExistingDirectoryIsNotNew),
    ("FileMgr removes temp files during startup", FileMgr_RemovesTempFiles),
    ("FileMgr reads and writes blocks", FileMgr_ReadWriteRoundTrip),
    ("FileMgr length tracks the highest written block", FileMgr_LengthTracksWrittenBlocks),
    ("FileMgr keeps file lengths isolated per file", FileMgr_LengthIsPerFile),
    ("LogMgr creates records and iterates in reverse", LogMgr_CreateAndIterate),
    ("BufferMgr available starts at pool size", BufferMgr_AvailableStartsAtPoolSize),
    ("BufferMgr pin decreases available", BufferMgr_PinDecreasesAvailable),
    ("BufferMgr unpin increases available", BufferMgr_UnpinIncreasesAvailable),
    ("BufferMgr pinning same block twice does not double-decrement available", BufferMgr_PinSameBlockNoDoubleDec),
    ("BufferMgr pin all buffers then pin another throws BufferAbortException", BufferMgr_PinExhaustedThrows),
    ("BufferMgr pinned buffer returns correct block", BufferMgr_PinnedBufferReturnsBlock),
    ("BufferMgr unpin then re-pin reuses buffer from pool", BufferMgr_UnpinThenRePinReuses),
};

var failures = new List<string>();
foreach (var (name, body) in tests)
{
    try
    {
        body();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
        Console.WriteLine($"FAIL {name}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("Test failures:");
    foreach (var failure in failures)
        Console.Error.WriteLine($"- {failure}");
    Environment.Exit(1);
}

Console.WriteLine();
Console.WriteLine($"All {tests.Length} tests passed.");

static void BlockId_ExposesConstructorValues()
{
    var block = new BlockId("test.tbl", 42);

    Assert.Equal("test.tbl", block.FileName());
    Assert.Equal(42, block.Number());
}

static void BlockId_EqualityAndHashCode()
{
    var left = new BlockId("data.tbl", 3);
    var same = new BlockId("data.tbl", 3);
    var differentFile = new BlockId("other.tbl", 3);
    var differentBlock = new BlockId("data.tbl", 4);

    Assert.True(left.Equals(same), "Expected equal block ids to compare equal.");
    Assert.True(same.Equals(left), "Expected equality to be symmetric.");
    Assert.False(left.Equals(differentFile), "Different file names should not be equal.");
    Assert.False(left.Equals(differentBlock), "Different block numbers should not be equal.");
    Assert.False(left.Equals(null), "A block id should not equal null.");
    Assert.Equal(left.GetHashCode(), same!.GetHashCode());
}

static void BlockId_ToStringFormat()
{
    var block = new BlockId("students.tbl", 7);

    Assert.Equal("[filestudents.tbl, block 7] ", block.ToString());
}

static void Page_IntRoundTrip()
{
    var page = new Page(64);

    page.SetInt(0, 123456789);
    page.SetInt(8, -17);

    Assert.Equal(123456789, page.GetInt(0));
    Assert.Equal(-17, page.GetInt(8));
}

static void Page_ByteRoundTripAndIsolation()
{
    var page = new Page(64);
    var bytes = new byte[] { 1, 2, 3, 4, 5 };

    page.SetBytes(4, bytes);
    bytes[0] = 99;

    var stored = page.GetBytes(4);
    stored[1] = 88;

    Assert.SequenceEqual(new byte[] { 1, 2, 3, 4, 5 }, page.GetBytes(4));
}

static void Page_StringRoundTrip()
{
    var page = new Page(64);

    page.SetString(12, "dbsharp");
    page.SetString(12, "db");

    Assert.Equal("db", page.GetString(12));
}

static void Page_MaxLengthMatchesCharsetContract()
{
    var expected = (10 + 1) * Encoding.ASCII.GetMaxByteCount(1);

    Assert.Equal(expected, Page.MaxLength(10));
}

static void FileMgr_CreatesMissingDirectory()
{
    var dbPath = UniqueDirectoryPath();
    var dbDirectory = new DirectoryInfo(dbPath);

    var fileMgr = new FileMgr(dbDirectory, 128);

    Assert.True(Directory.Exists(dbPath), "Constructor should create a missing database directory.");
    Assert.True(fileMgr.IsNew(), "Missing directories should be reported as new databases.");
    Assert.Equal(128, fileMgr.BlockSize());
}

static void FileMgr_ExistingDirectoryIsNotNew()
{
    var dbPath = CreateDirectory();

    var fileMgr = new FileMgr(new DirectoryInfo(dbPath), 256);

    Assert.False(fileMgr.IsNew(), "Existing directories should not be reported as new databases.");
    Assert.Equal(256, fileMgr.BlockSize());
}

static void FileMgr_RemovesTempFiles()
{
    var dbPath = CreateDirectory();
    File.WriteAllText(Path.Combine(dbPath, "temp123.tbl"), "delete me");
    File.WriteAllText(Path.Combine(dbPath, "users.tbl"), "keep me");

    _ = new FileMgr(new DirectoryInfo(dbPath), 128);

    Assert.False(File.Exists(Path.Combine(dbPath, "temp123.tbl")), "temp-prefixed files should be removed.");
    Assert.True(File.Exists(Path.Combine(dbPath, "users.tbl")), "Non-temp files should remain.");
}

static void FileMgr_ReadWriteRoundTrip()
{
    var dbPath = CreateDirectory();
    var fileMgr = new FileMgr(new DirectoryInfo(dbPath), 128);
    var block = new BlockId("data.tbl", 0);
    var writePage = new Page(128);
    writePage.SetInt(0, 99);
    writePage.SetString(8, "alpha");

    fileMgr.Write(block, writePage);

    var readPage = new Page(128);
    fileMgr.Read(block, readPage);

    Assert.Equal(99, readPage.GetInt(0));
    Assert.Equal("alpha", readPage.GetString(8));
}

static void FileMgr_LengthTracksWrittenBlocks()
{
    var dbPath = CreateDirectory();
    var fileMgr = new FileMgr(new DirectoryInfo(dbPath), 64);
    var page = new Page(64);
    page.SetInt(0, 7);

    Assert.Equal(0, fileMgr.Length("data.tbl"));

    fileMgr.Write(new BlockId("data.tbl", 2), page);

    Assert.Equal(3, fileMgr.Length("data.tbl"));
}

static void FileMgr_LengthIsPerFile()
{
    var dbPath = CreateDirectory();
    var fileMgr = new FileMgr(new DirectoryInfo(dbPath), 64);
    var page = new Page(64);
    page.SetInt(0, 1);

    fileMgr.Write(new BlockId("users.tbl", 0), page);
    fileMgr.Write(new BlockId("orders.tbl", 1), page);

    Assert.Equal(1, fileMgr.Length("users.tbl"));
    Assert.Equal(2, fileMgr.Length("orders.tbl"));
}

static void LogMgr_CreateAndIterate()
{
    var dbPath = CreateDirectory();
    var fm = new FileMgr(new DirectoryInfo(dbPath), 400);
    var lm = new LogMgr(fm, "simpledb.log");

    // --- createRecords(1, 35) ---
    for (int i = 1; i <= 35; i++)
    {
        byte[] rec = CreateLogRecord("record" + i, i + 100);
        lm.Append(rec);
    }

    // printLogRecords – verify all 35 records, newest first
    {
        var records = new List<(string s, int n)>();
        foreach (byte[] rec in lm.GetEnumerator())
        {
            var p = new Page(rec);
            string s = p.GetString(0);
            int npos = Page.MaxLength(s.Length);
            int val = p.GetInt(npos);
            records.Add((s, val));
        }
        Assert.Equal(35, records.Count);
        // newest record first (record35), oldest last (record1)
        Assert.Equal("record35", records[0].s);
        Assert.Equal(135, records[0].n);
        Assert.Equal("record1", records[records.Count - 1].s);
        Assert.Equal(101, records[records.Count - 1].n);
    }

    // --- createRecords(36, 70) ---
    for (int i = 36; i <= 70; i++)
    {
        byte[] rec = CreateLogRecord("record" + i, i + 100);
        lm.Append(rec);
    }
    lm.Flush(65);

    // printLogRecords – verify all 70 records
    {
        var records = new List<(string s, int n)>();
        foreach (byte[] rec in lm.GetEnumerator())
        {
            var p = new Page(rec);
            string s = p.GetString(0);
            int npos = Page.MaxLength(s.Length);
            int val = p.GetInt(npos);
            records.Add((s, val));
        }
        Assert.Equal(70, records.Count);
        Assert.Equal("record70", records[0].s);
        Assert.Equal(170, records[0].n);
        Assert.Equal("record1", records[records.Count - 1].s);
        Assert.Equal(101, records[records.Count - 1].n);
    }
}

static byte[] CreateLogRecord(string s, int n)
{
    int npos = Page.MaxLength(s.Length);
    byte[] b = new byte[npos + sizeof(int)];
    var p = new Page(b);
    p.SetString(0, s);
    p.SetInt(npos, n);
    return b;
}

static (FileMgr fm, LogMgr lm) CreateBufferTestDeps()
{
    var dbPath = CreateDirectory();
    var fm = new FileMgr(new DirectoryInfo(dbPath), 400);
    var lm = new LogMgr(fm, "simpledb.log");
    return (fm, lm);
}

static void BufferMgr_AvailableStartsAtPoolSize()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new BufferMgr(fm, lm, 3);

    Assert.Equal(3, bm.Available());
}

static void BufferMgr_PinDecreasesAvailable()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new BufferMgr(fm, lm, 3);

    // need a block on disk
    fm.Append("test.tbl");
    bm.Pin(new BlockId("test.tbl", 0));

    Assert.Equal(2, bm.Available());
}

static void BufferMgr_UnpinIncreasesAvailable()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new BufferMgr(fm, lm, 3);

    fm.Append("test.tbl");
    var buff = bm.Pin(new BlockId("test.tbl", 0));
    bm.Unpin(buff);

    Assert.Equal(3, bm.Available());
}

static void BufferMgr_PinSameBlockNoDoubleDec()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new BufferMgr(fm, lm, 3);

    fm.Append("test.tbl");
    var blk = new BlockId("test.tbl", 0);
    bm.Pin(blk);
    bm.Pin(blk); // same block, already pinned

    // should only have decreased by 1, not 2
    Assert.Equal(2, bm.Available());
}

static void BufferMgr_PinExhaustedThrows()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new BufferMgr(fm, lm, 2);

    fm.Append("test.tbl");
    fm.Append("test.tbl");
    fm.Append("test.tbl");

    bm.Pin(new BlockId("test.tbl", 0));
    bm.Pin(new BlockId("test.tbl", 1));

    bool threw = false;
    try
    {
        bm.Pin(new BlockId("test.tbl", 2));
    }
    catch (BufferAbortException)
    {
        threw = true;
    }
    Assert.True(threw, "Expected BufferAbortException when all buffers are pinned.");
}

static void BufferMgr_PinnedBufferReturnsBlock()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new BufferMgr(fm, lm, 3);

    fm.Append("test.tbl");
    var blk = new BlockId("test.tbl", 0);
    var buff = bm.Pin(blk);

    Assert.True(buff.Block().Equals(blk), "Pinned buffer should reference the requested block.");
}

static void BufferMgr_UnpinThenRePinReuses()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new BufferMgr(fm, lm, 1);

    fm.Append("test.tbl");
    fm.Append("test.tbl");

    // pin block 0, write data, unpin
    var blk0 = new BlockId("test.tbl", 0);
    var buff0 = bm.Pin(blk0);
    buff0.Contents().SetInt(0, 42);
    buff0.SetModified(1, 0);
    bm.Unpin(buff0);

    // pin block 1 (evicts block 0)
    var buff1 = bm.Pin(new BlockId("test.tbl", 1));
    bm.Unpin(buff1);

    // re-pin block 0 — should read back from disk
    var buff0Again = bm.Pin(blk0);
    Assert.Equal(42, buff0Again.Contents().GetInt(0));
}

static string CreateDirectory()
{
    var path = UniqueDirectoryPath();
    Directory.CreateDirectory(path);
    return path;
}

static string UniqueDirectoryPath()
{
    return Path.Combine(Path.GetTempPath(), $"dbsharp-tests-{Guid.NewGuid():N}");
}

static class Assert
{
    public static void Equal<T>(T? expected, T? actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}, but got {actual}.");
    }

    public static void True(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    public static void False(bool condition, string message)
    {
        if (condition)
            throw new InvalidOperationException(message);
    }

    public static void SequenceEqual(byte[] expected, byte[] actual)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                $"Expected [{string.Join(", ", expected)}], but got [{string.Join(", ", actual)}].");
        }
    }
}
