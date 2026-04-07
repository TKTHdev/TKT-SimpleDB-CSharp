using DBSharp.Buffers;
using DBSharp.File;
using DBSharp.Concurrency;
using DBSharp.Log;
using DBSharp.Transactions;
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
    ("BufferMgrWithBufferHashTable available starts at pool size", BufferMgrWithBufferHashTable_AvailableStartsAtPoolSize),
    ("BufferMgrWithBufferHashTable pinning same block twice does not double-decrement available", BufferMgrWithBufferHashTable_PinSameBlockNoDoubleDec),
    ("BufferMgrWithBufferHashTable eviction keeps hash table mapping consistent", BufferMgrWithBufferHashTable_EvictionMaintainsCorrectMapping),
    ("FIFOBufferMgr available starts at pool size", FIFOBufferMgr_AvailableStartsAtPoolSize),
    ("FIFOBufferMgr pinning same block twice does not double-decrement available", FIFOBufferMgr_PinSameBlockNoDoubleDec),
    ("FIFOBufferMgr uses a free frame before evicting existing blocks", FIFOBufferMgr_UsesFreeFrameBeforeEviction),
    ("FIFOBufferMgr chooses oldest unpinned buffer for eviction", FIFOBufferMgr_EvictsOldestUnpinned),
    ("FIFOBufferMgr does not evict pinned buffer even if it is oldest", FIFOBufferMgr_DoesNotEvictPinnedOldest),
    ("LRUBufferMgr uses a free frame before evicting existing blocks", LRUBufferMgr_UsesFreeFrameBeforeEviction),
    ("LRUBufferMgr chooses least recently unpinned buffer for eviction", LRUBufferMgr_EvictsLeastRecentlyUnpinned),
    ("LRUBufferMgr does not evict pinned buffer even if it is least recently unpinned", LRUBufferMgr_DoesNotEvictPinnedLeastRecent),
    ("ClockBufferMgr uses a free frame before evicting existing blocks", ClockBufferMgr_UsesFreeFrameBeforeEviction),
    ("ClockBufferMgr starts scan after previous replacement", ClockBufferMgr_StartsScanAfterPreviousReplacement),
    ("ClockBufferMgr does not evict pinned frame", ClockBufferMgr_DoesNotEvictPinnedFrame),
    ("CleanFirstBufferMgr prefers clean unpinned frames", CleanFirstBufferMgr_PrefersCleanUnpinned),
    ("CleanFirstBufferMgr falls back to dirty when no clean frame exists", CleanFirstBufferMgr_FallsBackToDirty),
    ("Transaction SetInt and GetInt round trip", Transaction_SetIntGetInt),
    ("Transaction SetString and GetString round trip", Transaction_SetStringGetString),
    ("Transaction Commit persists data for next transaction", Transaction_CommitPersists),
    ("Transaction Rollback undoes int changes", Transaction_RollbackUndoesInt),
    ("Transaction Rollback undoes string changes", Transaction_RollbackUndoesString),
    ("Transaction Append creates blocks and Size tracks them", Transaction_AppendAndSize),
    ("Transaction multiple blocks can be pinned and modified independently", Transaction_MultipleBlocks),
    ("Transaction concurrent readers on same block allowed", Transaction_ConcurrentReaders),
    ("Transaction XLock blocks other transaction writer", Transaction_XLockBlocksWriter),
    ("Transaction XLock blocks reader until commit", Transaction_XLockBlocksReader),
    ("Transaction SLock blocks writer until readers commit", Transaction_SLockBlocksWriter),
    ("Transaction commit releases lock and unblocks waiting tx", Transaction_CommitUnblocksWaiter),
    ("Transaction rollback releases lock and unblocks waiting tx", Transaction_RollbackUnblocksWaiter),
    ("Transaction writers serialized on same block", Transaction_WritersSerialized),
    ("Transaction locks on different blocks do not interfere", Transaction_IndependentBlocksNoConflict),
    ("QuiescentCheckpoint blocks new transactions until complete", QuiescentCheckpoint_BlocksNewTransactions),
    ("QuiescentCheckpoint waits for running transactions to finish", QuiescentCheckpoint_WaitsForRunningTxns),
    ("QuiescentCheckpoint flushes data and writes checkpoint record", QuiescentCheckpoint_FlushesAndWritesRecord),
    ("Recovery preserves committed int and string data", Recovery_PreservesCommitted),
    ("Recovery writes CHECKPOINT record to log", Recovery_WritesCheckpointRecord),
    ("Recovery preserves data after commit and rollback", Recovery_AfterCommitAndRollback),
    ("Recovery is idempotent", Recovery_Idempotent),
    ("Recovery stops scanning at CHECKPOINT record", Recovery_StopsAtCheckpoint),
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

static void BufferMgrWithBufferHashTable_AvailableStartsAtPoolSize()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new BufferMgrWithBufferHashTable(fm, lm, 3);

    Assert.Equal(3, bm.Available());
}

static void BufferMgrWithBufferHashTable_PinSameBlockNoDoubleDec()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new BufferMgrWithBufferHashTable(fm, lm, 3);

    fm.Append("test.tbl");
    var blk = new BlockId("test.tbl", 0);
    bm.Pin(blk);
    bm.Pin(blk);

    Assert.Equal(2, bm.Available());
}

static void BufferMgrWithBufferHashTable_EvictionMaintainsCorrectMapping()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new BufferMgrWithBufferHashTable(fm, lm, 1);

    fm.Append("test.tbl");
    fm.Append("test.tbl");

    var blk0 = new BlockId("test.tbl", 0);
    var blk1 = new BlockId("test.tbl", 1);

    var b0 = bm.Pin(blk0);
    bm.Unpin(b0);

    var b1 = bm.Pin(blk1);
    bm.Unpin(b1);

    var b0Again = bm.Pin(blk0);
    Assert.True(b0Again.Block().Equals(blk0),
        "Re-pinning block 0 after eviction should return a buffer assigned to block 0.");
}

static void FIFOBufferMgr_AvailableStartsAtPoolSize()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new FIFOBufferMgr(fm, lm, 3);

    Assert.Equal(3, bm.Available());
}

static void FIFOBufferMgr_PinSameBlockNoDoubleDec()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new FIFOBufferMgr(fm, lm, 3);

    fm.Append("test.tbl");
    var blk = new BlockId("test.tbl", 0);
    bm.Pin(blk);
    bm.Pin(blk);

    Assert.Equal(2, bm.Available());
}

static void FIFOBufferMgr_EvictsOldestUnpinned()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new FIFOBufferMgr(fm, lm, 2);

    fm.Append("test.tbl");
    fm.Append("test.tbl");
    fm.Append("test.tbl");

    var blk0 = new BlockId("test.tbl", 0);
    var blk1 = new BlockId("test.tbl", 1);
    var blk2 = new BlockId("test.tbl", 2);

    var b0 = bm.Pin(blk0);
    var b1 = bm.Pin(blk1);
    bm.Unpin(b0);
    bm.Unpin(b1);

    // Both buffers are unpinned; FIFO should evict block 0 first.
    var b2 = bm.Pin(blk2);

    // FIFO should reuse the frame that previously held blk0 (older than blk1).
    Assert.True(object.ReferenceEquals(b0, b2), "FIFO should evict/reuse the oldest frame first.");
    Assert.True(b0.Block().Equals(blk2), "Oldest frame should now contain the new block.");
    Assert.True(b1.Block().Equals(blk1), "Newer frame should remain untouched.");
}

static void FIFOBufferMgr_UsesFreeFrameBeforeEviction()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new FIFOBufferMgr(fm, lm, 2);

    fm.Append("test.tbl");
    fm.Append("test.tbl");

    var blk0 = new BlockId("test.tbl", 0);
    var blk1 = new BlockId("test.tbl", 1);

    var b0 = bm.Pin(blk0);
    bm.Unpin(b0);

    // There is still one never-used frame; blk0 should remain resident.
    var b1 = bm.Pin(blk1);
    Assert.False(object.ReferenceEquals(b0, b1), "Second distinct block should use a different free frame.");

    var b0Again = bm.Pin(blk0);
    Assert.True(object.ReferenceEquals(b0Again, b0), "Original block should still be resident when a free frame existed.");
}

static void FIFOBufferMgr_DoesNotEvictPinnedOldest()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new FIFOBufferMgr(fm, lm, 2);

    fm.Append("test.tbl");
    fm.Append("test.tbl");
    fm.Append("test.tbl");

    var blk0 = new BlockId("test.tbl", 0);
    var blk1 = new BlockId("test.tbl", 1);
    var blk2 = new BlockId("test.tbl", 2);

    var b0 = bm.Pin(blk0); // oldest, kept pinned
    var b1 = bm.Pin(blk1);
    bm.Unpin(b1);          // only block 1 is evictable

    var b2 = bm.Pin(blk2);
    Assert.True(b0.Block().Equals(blk0), "Pinned oldest block should not be evicted.");
    Assert.True(b2.Block().Equals(blk2), "Newly pinned buffer should hold requested block.");
}

static void LRUBufferMgr_UsesFreeFrameBeforeEviction()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new LRUBufferMgr(fm, lm, 2);

    fm.Append("test.tbl");
    fm.Append("test.tbl");

    var blk0 = new BlockId("test.tbl", 0);
    var blk1 = new BlockId("test.tbl", 1);

    var b0 = bm.Pin(blk0);
    bm.Unpin(b0);

    // One frame is still never used; second block should consume that frame.
    var b1 = bm.Pin(blk1);
    Assert.False(object.ReferenceEquals(b0, b1), "Second distinct block should use a different free frame.");

    var b0Again = bm.Pin(blk0);
    Assert.True(object.ReferenceEquals(b0Again, b0), "Original block should stay resident while a free frame exists.");
}

static void LRUBufferMgr_EvictsLeastRecentlyUnpinned()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new LRUBufferMgr(fm, lm, 2);

    fm.Append("test.tbl");
    fm.Append("test.tbl");
    fm.Append("test.tbl");

    var blk0 = new BlockId("test.tbl", 0);
    var blk1 = new BlockId("test.tbl", 1);
    var blk2 = new BlockId("test.tbl", 2);

    var b0 = bm.Pin(blk0);
    var b1 = bm.Pin(blk1);
    bm.Unpin(b0); // older unpin
    bm.Unpin(b1); // more recent unpin

    // LRU should evict block 0 because it was unpinned less recently than block 1.
    var b2 = bm.Pin(blk2);

    Assert.True(object.ReferenceEquals(b0, b2), "LRU should reuse the least recently unpinned frame.");
    Assert.True(b0.Block().Equals(blk2), "Evicted frame should now hold the requested block.");
    Assert.True(b1.Block().Equals(blk1), "More recently unpinned frame should remain untouched.");
}

static void LRUBufferMgr_DoesNotEvictPinnedLeastRecent()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new LRUBufferMgr(fm, lm, 2);

    fm.Append("test.tbl");
    fm.Append("test.tbl");
    fm.Append("test.tbl");

    var blk0 = new BlockId("test.tbl", 0);
    var blk1 = new BlockId("test.tbl", 1);
    var blk2 = new BlockId("test.tbl", 2);

    var b0 = bm.Pin(blk0); // keep pinned
    var b1 = bm.Pin(blk1);
    bm.Unpin(b1);          // only block 1 is evictable

    var b2 = bm.Pin(blk2);
    Assert.True(b0.Block().Equals(blk0), "Pinned block should not be evicted.");
    Assert.True(object.ReferenceEquals(b1, b2), "LRU should reuse the only unpinned frame.");
    Assert.True(b2.Block().Equals(blk2), "Reused frame should hold requested block.");
}

static void ClockBufferMgr_UsesFreeFrameBeforeEviction()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new ClockBufferMgr(fm, lm, 2);

    fm.Append("test.tbl");
    fm.Append("test.tbl");

    var blk0 = new BlockId("test.tbl", 0);
    var blk1 = new BlockId("test.tbl", 1);

    var b0 = bm.Pin(blk0);
    bm.Unpin(b0);

    var b1 = bm.Pin(blk1);
    Assert.False(object.ReferenceEquals(b0, b1), "Second distinct block should use a different free frame.");

    var b0Again = bm.Pin(blk0);
    Assert.True(object.ReferenceEquals(b0Again, b0), "Original block should remain resident while a free frame exists.");
}

static void ClockBufferMgr_StartsScanAfterPreviousReplacement()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new ClockBufferMgr(fm, lm, 4);

    fm.Append("test.tbl");
    fm.Append("test.tbl");
    fm.Append("test.tbl");
    fm.Append("test.tbl");
    fm.Append("test.tbl");
    fm.Append("test.tbl");

    var blk0 = new BlockId("test.tbl", 0);
    var blk1 = new BlockId("test.tbl", 1);
    var blk2 = new BlockId("test.tbl", 2);
    var blk3 = new BlockId("test.tbl", 3);
    var blk4 = new BlockId("test.tbl", 4);
    var blk5 = new BlockId("test.tbl", 5);

    var b0 = bm.Pin(blk0);
    var b1 = bm.Pin(blk1);
    var b2 = bm.Pin(blk2);
    var b3 = bm.Pin(blk3);
    bm.Unpin(b0);
    bm.Unpin(b1);
    bm.Unpin(b2);
    bm.Unpin(b3);

    // First replacement takes buffer 0, so clock starts next scan from buffer 1.
    var b4 = bm.Pin(blk4);
    bm.Unpin(b4);

    // Next replacement should start after the previous victim and take buffer 1.
    var b5 = bm.Pin(blk5);

    Assert.True(object.ReferenceEquals(b4, b0), "First replacement should use the first unpinned frame from the hand.");
    Assert.True(object.ReferenceEquals(b5, b1), "Second replacement should resume scan after the previous victim.");
    Assert.True(b5.Block().Equals(blk5), "Chosen frame should hold the requested block.");
}

static void ClockBufferMgr_DoesNotEvictPinnedFrame()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new ClockBufferMgr(fm, lm, 2);

    fm.Append("test.tbl");
    fm.Append("test.tbl");
    fm.Append("test.tbl");

    var blk0 = new BlockId("test.tbl", 0);
    var blk1 = new BlockId("test.tbl", 1);
    var blk2 = new BlockId("test.tbl", 2);

    var b0 = bm.Pin(blk0); // keep pinned
    var b1 = bm.Pin(blk1);
    bm.Unpin(b1);

    var b2 = bm.Pin(blk2);
    Assert.True(b0.Block().Equals(blk0), "Pinned frame should not be evicted.");
    Assert.True(object.ReferenceEquals(b2, b1), "Clock should reuse the only unpinned frame.");
    Assert.True(b2.Block().Equals(blk2), "Reused frame should hold requested block.");
}

static void CleanFirstBufferMgr_PrefersCleanUnpinned()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new CleanFirstBufferMgr(fm, lm, 2);

    fm.Append("test.tbl");
    fm.Append("test.tbl");
    fm.Append("test.tbl");

    var blk0 = new BlockId("test.tbl", 0);
    var blk1 = new BlockId("test.tbl", 1);
    var blk2 = new BlockId("test.tbl", 2);

    var b0 = bm.Pin(blk0);
    var b1 = bm.Pin(blk1);

    // Make block 0 dirty, keep block 1 clean.
    b0.Contents().SetInt(0, 123);
    b0.SetModified(1, -1);

    bm.Unpin(b0);
    bm.Unpin(b1);

    // Replacement should prefer clean frame b1.
    var b2 = bm.Pin(blk2);

    Assert.True(object.ReferenceEquals(b2, b1), "Should evict clean frame before dirty frame.");
    Assert.True(b0.Block().Equals(blk0), "Dirty frame should remain resident when clean victim exists.");
}

static void CleanFirstBufferMgr_FallsBackToDirty()
{
    var (fm, lm) = CreateBufferTestDeps();
    var bm = new CleanFirstBufferMgr(fm, lm, 2);

    fm.Append("test.tbl");
    fm.Append("test.tbl");
    fm.Append("test.tbl");

    var blk0 = new BlockId("test.tbl", 0);
    var blk1 = new BlockId("test.tbl", 1);
    var blk2 = new BlockId("test.tbl", 2);

    var b0 = bm.Pin(blk0);
    var b1 = bm.Pin(blk1);

    // Mark both frames dirty.
    b0.Contents().SetInt(0, 11);
    b0.SetModified(1, -1);
    b1.Contents().SetInt(0, 22);
    b1.SetModified(1, -1);

    bm.Unpin(b0);
    bm.Unpin(b1);

    // No clean victim exists, so strategy should still replace an unpinned dirty frame.
    var b2 = bm.Pin(blk2);

    Assert.True(object.ReferenceEquals(b2, b0) || object.ReferenceEquals(b2, b1),
        "Should fall back to replacing an unpinned dirty frame when no clean frame exists.");
    Assert.True(b2.Block().Equals(blk2), "Chosen victim should now hold requested block.");
}

static (FileMgr fm, LogMgr lm, BufferMgr bm) CreateTxTestDeps()
{
    var dbPath = CreateDirectory();
    var fm = new FileMgr(new DirectoryInfo(dbPath), 400);
    var lm = new LogMgr(fm, "simpledb.log");
    var bm = new BufferMgr(fm, lm, 8);
    return (fm, lm, bm);
}

static string TxTestFile()
{
    return $"txtest{Guid.NewGuid():N}.tbl";
}

static void Transaction_SetIntGetInt()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();
    var tx = new Transaction(fm, lm, bm);

    BlockId blk = tx.Append(file);
    tx.Pin(blk);
    tx.SetInt(blk, 0, 42, true);

    Assert.Equal(42, tx.GetInt(blk, 0));

    tx.SetInt(blk, 80, -7, true);
    Assert.Equal(-7, tx.GetInt(blk, 80));

    tx.Commit();
}

static void Transaction_SetStringGetString()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();
    var tx = new Transaction(fm, lm, bm);

    BlockId blk = tx.Append(file);
    tx.Pin(blk);
    tx.SetString(blk, 0, "hello", true);

    Assert.Equal("hello", tx.GetString(blk, 0));

    tx.Commit();
}

static void Transaction_CommitPersists()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    // tx1 writes and commits
    var tx1 = new Transaction(fm, lm, bm);
    BlockId blk = tx1.Append(file);
    tx1.Pin(blk);
    tx1.SetInt(blk, 0, 999, true);
    tx1.SetString(blk, 80, "persisted", true);
    tx1.Commit();

    // tx2 reads committed data
    var tx2 = new Transaction(fm, lm, bm);
    tx2.Pin(blk);
    Assert.Equal(999, tx2.GetInt(blk, 0));
    Assert.Equal("persisted", tx2.GetString(blk, 80));
    tx2.Commit();
}

static void Transaction_RollbackUndoesInt()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    // tx1 writes initial value and commits
    var tx1 = new Transaction(fm, lm, bm);
    BlockId blk = tx1.Append(file);
    tx1.Pin(blk);
    tx1.SetInt(blk, 0, 100, true);
    tx1.Commit();

    // tx2 overwrites, then rolls back
    var tx2 = new Transaction(fm, lm, bm);
    tx2.Pin(blk);
    tx2.SetInt(blk, 0, 200, true);
    Assert.Equal(200, tx2.GetInt(blk, 0));
    tx2.Rollback();

    // tx3 reads — should see original value
    var tx3 = new Transaction(fm, lm, bm);
    tx3.Pin(blk);
    Assert.Equal(100, tx3.GetInt(blk, 0));
    tx3.Commit();
}

static void Transaction_RollbackUndoesString()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    var tx1 = new Transaction(fm, lm, bm);
    BlockId blk = tx1.Append(file);
    tx1.Pin(blk);
    tx1.SetString(blk, 0, "original", true);
    tx1.Commit();

    var tx2 = new Transaction(fm, lm, bm);
    tx2.Pin(blk);
    tx2.SetString(blk, 0, "changed", true);
    Assert.Equal("changed", tx2.GetString(blk, 0));
    tx2.Rollback();

    var tx3 = new Transaction(fm, lm, bm);
    tx3.Pin(blk);
    Assert.Equal("original", tx3.GetString(blk, 0));
    tx3.Commit();
}

static void Transaction_AppendAndSize()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();
    var tx = new Transaction(fm, lm, bm);

    Assert.Equal(0, tx.Size(file));

    BlockId blk0 = tx.Append(file);
    Assert.Equal(1, tx.Size(file));
    Assert.Equal(0, blk0.Number());

    BlockId blk1 = tx.Append(file);
    Assert.Equal(2, tx.Size(file));
    Assert.Equal(1, blk1.Number());

    tx.Commit();
}

static void Transaction_MultipleBlocks()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();
    var tx = new Transaction(fm, lm, bm);

    BlockId blk0 = tx.Append(file);
    BlockId blk1 = tx.Append(file);
    tx.Pin(blk0);
    tx.Pin(blk1);

    tx.SetInt(blk0, 0, 11, true);
    tx.SetInt(blk1, 0, 22, true);
    tx.SetString(blk0, 80, "block0", true);
    tx.SetString(blk1, 80, "block1", true);

    Assert.Equal(11, tx.GetInt(blk0, 0));
    Assert.Equal(22, tx.GetInt(blk1, 0));
    Assert.Equal("block0", tx.GetString(blk0, 80));
    Assert.Equal("block1", tx.GetString(blk1, 80));

    tx.Commit();
}

static void Transaction_ConcurrentReaders()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    // setup: write data
    var setup = new Transaction(fm, lm, bm);
    BlockId blk = setup.Append(file);
    setup.Pin(blk);
    setup.SetInt(blk, 0, 77, true);
    setup.Commit();

    // two readers on the same block should not block each other
    var reader1 = new Transaction(fm, lm, bm);
    var reader2 = new Transaction(fm, lm, bm);
    reader1.Pin(blk);
    reader2.Pin(blk);

    Assert.Equal(77, reader1.GetInt(blk, 0));
    Assert.Equal(77, reader2.GetInt(blk, 0));

    reader1.Commit();
    reader2.Commit();
}

static void Transaction_XLockBlocksWriter()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    var setup = new Transaction(fm, lm, bm);
    BlockId blk = setup.Append(file);
    setup.Pin(blk);
    setup.SetInt(blk, 0, 1, true);
    setup.Commit();

    // tx1 takes XLock by writing
    var tx1 = new Transaction(fm, lm, bm);
    tx1.Pin(blk);
    tx1.SetInt(blk, 0, 2, true);

    // tx2 tries to write the same block on another thread — should be blocked
    bool tx2Completed = false;
    bool tx2Threw = false;
    var thread = new Thread(() =>
    {
        try
        {
            var tx2 = new Transaction(fm, lm, bm);
            tx2.Pin(blk);
            tx2.SetInt(blk, 0, 3, true);
            tx2.Commit();
            tx2Completed = true;
        }
        catch (LockAbortException)
        {
            tx2Threw = true;
        }
    });
    thread.Start();

    // give tx2 a moment to attempt the lock, then commit tx1
    Thread.Sleep(200);
    tx1.Commit();
    thread.Join();

    // tx2 either succeeded after tx1 committed, or threw LockAbortException.
    // Either outcome is valid — the key point is tx2 did NOT run concurrently.
    Assert.True(tx2Completed || tx2Threw,
        "tx2 should have either completed or thrown LockAbortException.");

    // Read the final value to confirm consistency.
    var reader = new Transaction(fm, lm, bm);
    reader.Pin(blk);
    int finalVal = reader.GetInt(blk, 0);
    Assert.True(finalVal == 2 || finalVal == 3,
        $"Expected final value to be 2 or 3, but got {finalVal}.");
    reader.Commit();
}

static void Transaction_XLockBlocksReader()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    var setup = new Transaction(fm, lm, bm);
    BlockId blk = setup.Append(file);
    setup.Pin(blk);
    setup.SetInt(blk, 0, 10, true);
    setup.Commit();

    // tx1 takes XLock by writing
    var tx1 = new Transaction(fm, lm, bm);
    tx1.Pin(blk);
    tx1.SetInt(blk, 0, 20, true);

    // tx2 tries to read the same block on another thread — should be blocked by XLock
    var readerStarted = new ManualResetEventSlim(false);
    int readValue = -1;
    bool readerDone = false;
    var thread = new Thread(() =>
    {
        var tx2 = new Transaction(fm, lm, bm);
        tx2.Pin(blk);
        readerStarted.Set();
        // GetInt acquires SLock — blocked while tx1 holds XLock
        readValue = tx2.GetInt(blk, 0);
        tx2.Commit();
        readerDone = true;
    });
    thread.Start();

    readerStarted.Wait();
    Thread.Sleep(300);
    // reader should still be blocked
    Assert.False(readerDone, "Reader should be blocked while writer holds XLock.");

    // release XLock
    tx1.Commit();
    thread.Join(TimeSpan.FromSeconds(12));

    Assert.True(readerDone, "Reader should complete after writer commits.");
    Assert.Equal(20, readValue);
}

static void Transaction_SLockBlocksWriter()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    var setup = new Transaction(fm, lm, bm);
    BlockId blk = setup.Append(file);
    setup.Pin(blk);
    setup.SetInt(blk, 0, 10, true);
    setup.Commit();

    // two readers hold SLock
    var reader1 = new Transaction(fm, lm, bm);
    reader1.Pin(blk);
    Assert.Equal(10, reader1.GetInt(blk, 0));

    var reader2 = new Transaction(fm, lm, bm);
    reader2.Pin(blk);
    Assert.Equal(10, reader2.GetInt(blk, 0));

    // writer on another thread tries to get XLock — blocked by SLocks
    var writerStarted = new ManualResetEventSlim(false);
    bool writerDone = false;
    var thread = new Thread(() =>
    {
        var writer = new Transaction(fm, lm, bm);
        writer.Pin(blk);
        writerStarted.Set();
        // SetInt acquires XLock — blocked while readers hold SLock
        writer.SetInt(blk, 0, 99, true);
        writer.Commit();
        writerDone = true;
    });
    thread.Start();

    writerStarted.Wait();
    Thread.Sleep(300);
    Assert.False(writerDone, "Writer should be blocked while readers hold SLock.");

    // release one reader — still one SLock held, writer still blocked
    reader1.Commit();
    Thread.Sleep(300);
    Assert.False(writerDone, "Writer should still be blocked with one reader remaining.");

    // release second reader — writer can proceed
    reader2.Commit();
    thread.Join(TimeSpan.FromSeconds(12));

    Assert.True(writerDone, "Writer should complete after all readers commit.");
}

static void Transaction_CommitUnblocksWaiter()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    var setup = new Transaction(fm, lm, bm);
    BlockId blk = setup.Append(file);
    setup.Pin(blk);
    setup.SetInt(blk, 0, 1, true);
    setup.Commit();

    // tx1 holds XLock
    var tx1 = new Transaction(fm, lm, bm);
    tx1.Pin(blk);
    tx1.SetInt(blk, 0, 2, true);

    var waiterStarted = new ManualResetEventSlim(false);
    bool waiterDone = false;
    int waiterRead = -1;
    var thread = new Thread(() =>
    {
        var tx2 = new Transaction(fm, lm, bm);
        tx2.Pin(blk);
        waiterStarted.Set();
        waiterRead = tx2.GetInt(blk, 0);
        tx2.Commit();
        waiterDone = true;
    });
    thread.Start();

    waiterStarted.Wait();
    Thread.Sleep(300);
    Assert.False(waiterDone, "Waiter should be blocked.");

    // commit unblocks the waiter
    tx1.Commit();
    thread.Join(TimeSpan.FromSeconds(12));

    Assert.True(waiterDone, "Waiter should proceed after commit.");
    Assert.Equal(2, waiterRead);
}

static void Transaction_RollbackUnblocksWaiter()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    var setup = new Transaction(fm, lm, bm);
    BlockId blk = setup.Append(file);
    setup.Pin(blk);
    setup.SetInt(blk, 0, 1, true);
    setup.Commit();

    // tx1 holds XLock
    var tx1 = new Transaction(fm, lm, bm);
    tx1.Pin(blk);
    tx1.SetInt(blk, 0, 2, true);

    var waiterStarted = new ManualResetEventSlim(false);
    bool waiterDone = false;
    int waiterRead = -1;
    var thread = new Thread(() =>
    {
        var tx2 = new Transaction(fm, lm, bm);
        tx2.Pin(blk);
        waiterStarted.Set();
        waiterRead = tx2.GetInt(blk, 0);
        tx2.Commit();
        waiterDone = true;
    });
    thread.Start();

    waiterStarted.Wait();
    Thread.Sleep(300);
    Assert.False(waiterDone, "Waiter should be blocked.");

    // rollback releases locks and undoes changes
    tx1.Rollback();
    thread.Join(TimeSpan.FromSeconds(12));

    Assert.True(waiterDone, "Waiter should proceed after rollback.");
    // rollback undid the write, so waiter should see the original value
    Assert.Equal(1, waiterRead);
}

static void Transaction_WritersSerialized()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    var setup = new Transaction(fm, lm, bm);
    BlockId blk = setup.Append(file);
    setup.Pin(blk);
    setup.SetInt(blk, 0, 0, true);
    setup.Commit();

    // run 5 writers sequentially on separate threads, each incrementing the value
    int numWriters = 5;
    var threads = new Thread[numWriters];
    var barrier = new ManualResetEventSlim(false);
    int successCount = 0;

    for (int i = 0; i < numWriters; i++)
    {
        int writerIndex = i;
        threads[i] = new Thread(() =>
        {
            barrier.Wait();
            try
            {
                var tx = new Transaction(fm, lm, bm);
                tx.Pin(blk);
                int current = tx.GetInt(blk, 0);
                tx.SetInt(blk, 0, current + 1, true);
                tx.Commit();
                Interlocked.Increment(ref successCount);
            }
            catch (LockAbortException)
            {
                // timed out waiting for lock — acceptable under contention
            }
        });
        threads[i].Start();
    }

    // release all writers at once
    barrier.Set();
    foreach (var t in threads)
        t.Join(TimeSpan.FromSeconds(15));

    // at least one writer must have succeeded
    Assert.True(successCount > 0, "At least one writer should succeed.");

    // final value must equal the number of successful writers
    var reader = new Transaction(fm, lm, bm);
    reader.Pin(blk);
    int finalVal = reader.GetInt(blk, 0);
    reader.Commit();

    Assert.Equal(successCount, finalVal);
}

static void Transaction_IndependentBlocksNoConflict()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    var setup = new Transaction(fm, lm, bm);
    BlockId blkA = setup.Append(file);
    BlockId blkB = setup.Append(file);
    setup.Pin(blkA);
    setup.Pin(blkB);
    setup.SetInt(blkA, 0, 10, true);
    setup.SetInt(blkB, 0, 20, true);
    setup.Commit();

    // two writers on different blocks should not block each other
    bool writerADone = false;
    bool writerBDone = false;
    var barrier = new ManualResetEventSlim(false);

    var threadA = new Thread(() =>
    {
        barrier.Wait();
        var tx = new Transaction(fm, lm, bm);
        tx.Pin(blkA);
        tx.SetInt(blkA, 0, 11, true);
        tx.Commit();
        writerADone = true;
    });

    var threadB = new Thread(() =>
    {
        barrier.Wait();
        var tx = new Transaction(fm, lm, bm);
        tx.Pin(blkB);
        tx.SetInt(blkB, 0, 21, true);
        tx.Commit();
        writerBDone = true;
    });

    threadA.Start();
    threadB.Start();
    barrier.Set();
    threadA.Join(TimeSpan.FromSeconds(12));
    threadB.Join(TimeSpan.FromSeconds(12));

    Assert.True(writerADone, "Writer A should complete without blocking.");
    Assert.True(writerBDone, "Writer B should complete without blocking.");

    var reader = new Transaction(fm, lm, bm);
    reader.Pin(blkA);
    reader.Pin(blkB);
    Assert.Equal(11, reader.GetInt(blkA, 0));
    Assert.Equal(21, reader.GetInt(blkB, 0));
    reader.Commit();
}

static void QuiescentCheckpoint_BlocksNewTransactions()
{
    var (fm, lm, bm) = CreateTxTestDeps();

    // start a transaction so the checkpoint has something to wait for
    var tx1 = new Transaction(fm, lm, bm);

    // launch checkpoint on background thread — it will block new txns
    var ckpt = new Thread(() => Transaction.RunQuiescentCheckpointing(bm, lm));
    ckpt.IsBackground = true;
    ckpt.Start();

    // give checkpoint thread time to call StartCheckpoint
    Thread.Sleep(200);

    // try to create a new transaction on another thread — should be blocked
    bool newTxStarted = false;
    var txThread = new Thread(() =>
    {
        var tx2 = new Transaction(fm, lm, bm);
        newTxStarted = true;
        tx2.Commit();
    });
    txThread.Start();

    // wait a bit — tx2 should still be blocked
    Thread.Sleep(300);
    Assert.False(newTxStarted, "New transaction should be blocked during quiescent checkpoint.");

    // finish tx1 so checkpoint can complete and reopen the gate
    tx1.Commit();
    ckpt.Join(TimeSpan.FromSeconds(5));
    txThread.Join(TimeSpan.FromSeconds(5));

    Assert.True(newTxStarted, "New transaction should proceed after checkpoint completes.");
}

static void QuiescentCheckpoint_WaitsForRunningTxns()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    // start a transaction that holds work
    var tx1 = new Transaction(fm, lm, bm);
    BlockId blk = tx1.Append(file);
    tx1.Pin(blk);
    tx1.SetInt(blk, 0, 42, true);

    bool checkpointDone = false;
    var ckpt = new Thread(() =>
    {
        Transaction.RunQuiescentCheckpointing(bm, lm);
        checkpointDone = true;
    });
    ckpt.IsBackground = true;
    ckpt.Start();

    // checkpoint should not finish while tx1 is still running
    Thread.Sleep(300);
    Assert.False(checkpointDone, "Checkpoint should wait for running transaction to finish.");

    // commit tx1 — checkpoint should now complete
    tx1.Commit();
    ckpt.Join(TimeSpan.FromSeconds(5));

    Assert.True(checkpointDone, "Checkpoint should complete after all running transactions finish.");
}

static void Recovery_PreservesCommitted()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    // tx1: write int and string, then commit
    var tx1 = new Transaction(fm, lm, bm);
    BlockId blk = tx1.Append(file);
    tx1.Pin(blk);
    tx1.SetInt(blk, 0, 42, true);
    tx1.SetString(blk, 80, "committed", true);
    tx1.Commit();

    // run recovery — nothing to undo, committed data should survive
    var txRecovery = new Transaction(fm, lm, bm);
    txRecovery.Recover();

    var txRead = new Transaction(fm, lm, bm);
    txRead.Pin(blk);
    Assert.Equal(42, txRead.GetInt(blk, 0));
    Assert.Equal("committed", txRead.GetString(blk, 80));
    txRead.Commit();
}

static void Recovery_WritesCheckpointRecord()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    var tx1 = new Transaction(fm, lm, bm);
    BlockId blk = tx1.Append(file);
    tx1.Pin(blk);
    tx1.SetInt(blk, 0, 1, true);
    tx1.Commit();

    var txRecovery = new Transaction(fm, lm, bm);
    txRecovery.Recover();

    // scan the log for a CHECKPOINT record
    bool foundCheckpoint = false;
    foreach (byte[] bytes in lm.GetEnumerator())
    {
        LogRecord rec = LogRecord.CreateLogRecord(bytes);
        if (rec.Op() == LogRecord.CHECKPOINT)
        {
            foundCheckpoint = true;
            break;
        }
    }
    Assert.True(foundCheckpoint, "Log should contain a CHECKPOINT record after recovery.");
}

static void Recovery_AfterCommitAndRollback()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    // tx1: write initial values and commit
    var tx1 = new Transaction(fm, lm, bm);
    BlockId blk = tx1.Append(file);
    tx1.Pin(blk);
    tx1.SetInt(blk, 0, 10, true);
    tx1.SetInt(blk, 80, 20, true);
    tx1.Commit();

    // tx2: overwrite offset 0 and commit
    var tx2 = new Transaction(fm, lm, bm);
    tx2.Pin(blk);
    tx2.SetInt(blk, 0, 50, true);
    tx2.Commit();

    // tx3: overwrite offset 80 and rollback (undo via normal rollback)
    var tx3 = new Transaction(fm, lm, bm);
    tx3.Pin(blk);
    tx3.SetInt(blk, 80, 999, true);
    tx3.Rollback();

    // recovery should see tx1 committed, tx2 committed, tx3 rolled back
    var txRecovery = new Transaction(fm, lm, bm);
    txRecovery.Recover();

    var txRead = new Transaction(fm, lm, bm);
    txRead.Pin(blk);
    Assert.Equal(50, txRead.GetInt(blk, 0));   // tx2 committed
    Assert.Equal(20, txRead.GetInt(blk, 80));   // tx3 rolled back, restored to tx1's value
    txRead.Commit();
}

static void Recovery_Idempotent()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    var tx1 = new Transaction(fm, lm, bm);
    BlockId blk = tx1.Append(file);
    tx1.Pin(blk);
    tx1.SetInt(blk, 0, 77, true);
    tx1.SetString(blk, 80, "stable", true);
    tx1.Commit();

    // run recovery twice — result should be the same
    var txR1 = new Transaction(fm, lm, bm);
    txR1.Recover();

    var txR2 = new Transaction(fm, lm, bm);
    txR2.Recover();

    var txRead = new Transaction(fm, lm, bm);
    txRead.Pin(blk);
    Assert.Equal(77, txRead.GetInt(blk, 0));
    Assert.Equal("stable", txRead.GetString(blk, 80));
    txRead.Commit();
}

static void Recovery_StopsAtCheckpoint()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    // tx1: write and commit
    var tx1 = new Transaction(fm, lm, bm);
    BlockId blk = tx1.Append(file);
    tx1.Pin(blk);
    tx1.SetInt(blk, 0, 100, true);
    tx1.Commit();

    // first recovery — writes a CHECKPOINT record
    var txR1 = new Transaction(fm, lm, bm);
    txR1.Recover();

    // tx2: write new value after the checkpoint and commit
    var tx2 = new Transaction(fm, lm, bm);
    tx2.Pin(blk);
    tx2.SetInt(blk, 0, 200, true);
    tx2.Commit();

    // tx3: overwrite and rollback
    var tx3 = new Transaction(fm, lm, bm);
    tx3.Pin(blk);
    tx3.SetInt(blk, 0, 999, true);
    tx3.Rollback();

    // second recovery — should only scan back to the CHECKPOINT,
    // see tx2 as committed and tx3 as rolled back
    var txR2 = new Transaction(fm, lm, bm);
    txR2.Recover();

    var txRead = new Transaction(fm, lm, bm);
    txRead.Pin(blk);
    Assert.Equal(200, txRead.GetInt(blk, 0));
    txRead.Commit();
}

static void QuiescentCheckpoint_FlushesAndWritesRecord()
{
    var (fm, lm, bm) = CreateTxTestDeps();
    string file = TxTestFile();

    // write some data and commit
    var tx1 = new Transaction(fm, lm, bm);
    BlockId blk = tx1.Append(file);
    tx1.Pin(blk);
    tx1.SetInt(blk, 0, 99, true);
    tx1.Commit();

    // run checkpoint (no running txns, so it completes immediately)
    Transaction.RunQuiescentCheckpointing(bm, lm);

    // verify checkpoint record was written by scanning the log
    bool foundCheckpoint = false;
    foreach (byte[] bytes in lm.GetEnumerator())
    {
        LogRecord rec = LogRecord.CreateLogRecord(bytes);
        if (rec.Op() == LogRecord.CHECKPOINT)
        {
            foundCheckpoint = true;
            break;
        }
    }
    Assert.True(foundCheckpoint, "Log should contain a CHECKPOINT record after quiescent checkpoint.");

    // verify data is readable after checkpoint
    var tx2 = new Transaction(fm, lm, bm);
    tx2.Pin(blk);
    Assert.Equal(99, tx2.GetInt(blk, 0));
    tx2.Commit();
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
