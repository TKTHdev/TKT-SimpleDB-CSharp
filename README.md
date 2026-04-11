# DBSharp

DBSharp is a work-in-progress educational database engine in C# (.NET 8).
The implementation covers storage-layer fundamentals through transaction processing:

- fixed-size block file I/O
- in-page primitive serialization
- append-only log records with forward and backward iteration
- buffer pool pin/unpin management with multiple replacement policies
- WAL-based recovery (undo-only and undo-redo)
- concurrency control with shared/exclusive locks and deadlock prevention (wait-die, wound-wait)
- quiescent and non-quiescent checkpointing
- transactions with commit, rollback, and crash recovery

This repository is not yet a full DBMS. It currently provides core building blocks from storage through transaction management.

## Status

Implemented and covered by local tests (76 passing cases):

- `File.BlockId`
  - block identity (file name + block number), equality, hash, formatting
- `File.Page`
  - read/write for `int`, `short`, `bool`, `DateTime`, `byte[]`, `string`
  - fixed byte array backing and bounds checks
- `File.FileMgr`
  - block read/write/append/truncate
  - file length in blocks
  - startup cleanup for files prefixed with `temp`
  - I/O statistics tracking (blocks read, written, appended)
- `Log.LogMgr` + `Log.BackwardLogIterator` + `Log.ForwardLogIterator`
  - append log records into block pages
  - flush control with LSN tracking
  - iterate records from newest to oldest (backward) or oldest to newest (forward)
- `Log.LogRecord`
  - typed log records: Checkpoint, Start, Commit, Rollback, SetInt, SetString, NQCheckpoint, Append
  - factory method to deserialize log bytes into the appropriate record type
  - undo and redo support per record type
- `Log.UndoOnlyRecoveryMgr`
  - WAL-based undo-only recovery (force policy — dirty buffers flushed at commit)
  - rollback (undo single transaction) and recover (undo all uncommitted)
  - old-value logging for SetInt/SetString operations
  - handles both quiescent and non-quiescent checkpoints
- `Log.UndoRedoRecoveryMgr`
  - WAL-based undo-redo recovery (no-force policy — buffers not forced at commit)
  - two-pass recovery: undo pass (backward) then redo pass (forward)
  - old-value and new-value logging for SetInt/SetString operations
- `Buffer.Buffer` + `Buffer.AbstractBufferMgr` + replacement policy variants
  - pin/unpin workflow
  - buffer availability accounting
  - replacement policies:
    - basic unpinned-buffer selection (`BufferMgr`)
    - FIFO eviction among unpinned frames (`FIFOBufferMgr`)
    - LRU eviction by least-recently-unpinned frame (`LRUBufferMgr`)
    - Clock (second-chance) sweep (`ClockBufferMgr`)
    - clean-first preference with dirty fallback (`CleanFirstBufferMgr`)
    - LSN-based: clean-first, then lowest-LSN dirty (`LSNBasedBufferMgr`)
  - hash-table-based buffer lookup (`BufferMgrWithBufferHashTable`)
  - timeout + `BufferAbortException` when exhausted
- `Concurrency.ConcurrencyMgr` + `Concurrency.ILockTable`
  - shared (S) and exclusive (X) lock protocol
  - S-lock upgrades to X-lock via lock escalation
  - pluggable deadlock prevention via `ILockTable`:
    - wait-die protocol (`WaitDieLockTable`)
    - wound-wait protocol (`WoundWaitLockTable`)
  - `LockAbortException` on conflict
- `Checkpoint.ICheckpointStrategy`
  - quiescent checkpoint: blocks new transactions, waits for active ones to finish (`QuiescentCheckpointStrategy`)
  - non-quiescent checkpoint: snapshots active transactions without blocking (`NonQuiescentCheckpointStrategy`)
- `Transaction.Transaction` + `Transaction.BufferList`
  - transactional read/write of int and string values
  - commit, rollback, and crash recovery
  - per-transaction buffer pin tracking
  - concurrency-safe block append, truncate, and size queries
  - EOF sentinel locking for phantom prevention
  - selectable recovery strategy (undo-only or undo-redo)

## Repository Layout

```text
DBSharp/
├── Buffer/
│   ├── Buffer.cs
│   ├── AbstractBufferMgr.cs      # template-method base for replacement policies
│   ├── BufferMgr.cs              # naive replacement
│   ├── FIFOBufferMgr.cs
│   ├── LRUBufferMgr.cs
│   ├── ClockBufferMgr.cs
│   ├── CleanFirstBufferMgr.cs
│   ├── LSNBasedBufferMgr.cs
│   ├── BufferMgrWithBufferHashTable.cs
│   ├── IBufferMgr.cs
│   └── BufferAbortException.cs
├── Checkpoint/
│   ├── ICheckpointStrategy.cs
│   ├── QuiescentCheckpointStrategy.cs
│   └── NonQuiescentCheckpointStrategy.cs
├── Concurrency/
│   ├── ConcurrencyMgr.cs
│   ├── ILockTable.cs
│   ├── WaitDieLockTable.cs
│   ├── WoundWaitLockTable.cs
│   └── LockAbortException.cs
├── File/
│   ├── BlockId.cs
│   ├── FileMgr.cs
│   └── Page.cs
├── Log/
│   ├── LogMgr.cs
│   ├── BackwardLogIterator.cs
│   ├── ForwardLogIterator.cs
│   ├── LogRecord.cs
│   ├── IRecoveryMgr.cs
│   ├── UndoOnlyRecoveryMgr.cs
│   └── UndoRedoRecoveryMgr.cs
├── Transaction/
│   ├── Transaction.cs
│   └── BufferList.cs
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
using DBSharp.Buffers;
using DBSharp.Transactions;

var fm = new FileMgr(new DirectoryInfo("mydb"), blocksize: 400);
var lm = new LogMgr(fm, "simpledb.log");
var bm = new BufferMgr(fm, lm, numbuffs: 8);

// Use a transaction to read and write data
var tx = new Transaction(fm, lm, bm);
fm.Append("data.tbl");
var blk = new BlockId("data.tbl", 0);

tx.Pin(blk);
tx.SetInt(blk, 0, 123, okToLog: true);
tx.SetString(blk, 4, "hello", okToLog: true);
tx.Commit();
```

## Notes

- API design is still evolving.
- This project is intended for learning and incremental DB engine development.

## Japanese README

Japanese version: [README.ja.md](./README.ja.md)
