# DBSharp

DBSharp is a work-in-progress educational database engine in C# (.NET 8).
The current implementation focuses on storage-layer fundamentals:

- fixed-size block file I/O
- in-page primitive serialization
- append-only log records with reverse iteration
- buffer pool pin/unpin management

This repository is not yet a full DBMS. It currently provides core building blocks.

## Status

Implemented and covered by local tests (29 passing cases):

- `File.BlockId`
  - block identity (file name + block number), equality, hash, formatting
- `File.Page`
  - read/write for `int`, `short`, `bool`, `DateTime`, `byte[]`, `string`
  - fixed byte array backing and bounds checks
- `File.FileMgr`
  - block read/write/append
  - file length in blocks
  - startup cleanup for files prefixed with `temp`
- `Log.LogMgr` + `Log.LogIterator`
  - append log records into block pages
  - flush control with LSN tracking
  - iterate records from newest to oldest
- `Buffer.Buffer` + `Buffer.BufferMgr` + `Buffer.FIFOBufferMgr` + `Buffer.LRUBufferMgr`
  - pin/unpin workflow
  - buffer availability accounting
  - replacement policies:
    - basic unpinned-buffer selection (`BufferMgr`)
    - FIFO eviction among unpinned frames (`FIFOBufferMgr`)
    - LRU eviction by least-recently-unpinned frame (`LRUBufferMgr`)
  - timeout + `BufferAbortException` when exhausted

## Repository Layout

```text
DBSharp/
├── Buffer/
│   ├── Buffer.cs
│   ├── BufferMgr.cs
│   └── BufferAbortException.cs
├── File/
│   ├── BlockId.cs
│   ├── FileMgr.cs
│   └── Page.cs
├── Log/
│   ├── LogMgr.cs
│   └── LogIterator.cs
└── DBSharp.Tests/
    └── Program.cs
```

## Requirements

- .NET SDK 8.0+

## Build

```bash
dotnet build
```

## Run Tests

The test project is a lightweight console runner (not xUnit/NUnit).

```bash
dotnet run --project DBSharp.Tests/DBSharp.Tests.csproj
```

## Quick Usage Example

```csharp
using DBSharp.File;
using DBSharp.Log;
using DBSharp.Buffer;

var fm = new FileMgr(new DirectoryInfo("mydb"), blocksize: 400);
var lm = new LogMgr(fm, "simpledb.log");
var bm = new BufferMgr(fm, lm, numbuffs: 8);

// Ensure one block exists
fm.Append("data.tbl");
var blk = new BlockId("data.tbl", 0);

// Pin a buffer, modify page content, and unpin
var buff = bm.Pin(blk);
buff.Contents().SetInt(0, 123);
buff.SetModified(txnum: 1, lsn: lm.Append(new byte[] { 1, 2, 3 }));
bm.Unpin(buff);
```

## Notes

- API design is still evolving.
- Threading/concurrency behavior is currently minimal and foundational.
- This project is intended for learning and incremental DB engine development.

## Japanese README

Japanese version: [README.ja.md](./README.ja.md)
