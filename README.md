# DBSharp

DBSharp is a work-in-progress educational database engine in C# (.NET 8).
The implementation covers storage-layer fundamentals through transaction processing
and now exposes a JDBC-style client API (both embedded and network):

- fixed-size block file I/O
- in-page primitive serialization
- append-only log records with forward and backward iteration
- buffer pool pin/unpin management with multiple replacement policies
- WAL-based recovery (undo-only and undo-redo)
- concurrency control with shared/exclusive locks and deadlock prevention (wait-die, wound-wait)
- quiescent and non-quiescent checkpointing
- transactions with commit, rollback, and crash recovery
- record manager, metadata catalog, scans, predicates, query/update planner
- **JDBC-style client API** (`DBSharp.Jdbc`) with both **embedded** and **network/RMI-style** modes
- **`SimpleDB` facade class** that wires up FileMgr, LogMgr, BufferMgr, MetadataMgr, and Planner

## Status

Implemented and covered by local tests:

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
- `SimpleDB` (top-level facade)
  - one-call setup of FileMgr, LogMgr, BufferMgr, MetadataMgr, and Planner
  - automatic crash recovery on existing databases
  - transaction factory (`NewTx()`)
- `Jdbc` (JDBC-style API surface)
  - core interfaces: `IDriver`, `IConnection`, `IStatement`, `IResultSet`, `IResultSetMetaData`
  - default-throwing adapter base classes:
    `DriverAdapter`, `ConnectionAdapter`, `StatementAdapter`, `ResultSetAdapter`, `ResultSetMetaDataAdapter`
- `Jdbc.Embedded` (in-process JDBC implementation)
  - `EmbeddedDriver` boots a `SimpleDB` from a directory path
  - `EmbeddedConnection` owns a current `Transaction`; auto-commits on `Close()`
  - `EmbeddedStatement` calls `Planner.ExecuteUpdate` / `CreateQueryPlan`,
    commits on success, rolls back on exception
  - `EmbeddedResultSet` wraps `IScan`+`Schema`; commits the surrounding tx on `Close()`
  - `EmbeddedMetaData` exposes column count, names, SQL types, and display sizes
- `Jdbc.Network` (network/RMI-style JDBC implementation, TCP-based)
  - server side: `SimpleDbServer` (TCP listener, default port `1099`),
    `RemoteConnectionImpl`, `RemoteStatementImpl`, `RemoteResultSetImpl`, `RemoteMetaDataImpl`
  - protocol: line-based text protocol (`QUERY`, `UPDATE`, `NEXT`, `GETINT`, `GETSTRING`,
    `METADATA`, `CLOSERS`, `CLOSE`); errors travel as `ERROR <message>` lines
  - client side: `NetworkDriver` and `RemoteConnectionStub`/`RemoteStatementStub`/
    `RemoteResultSetStub`/`RemoteMetaDataStub` proxy the same interfaces over TCP
  - client wrappers: `NetworkConnection`, `NetworkStatement`, `NetworkResultSet`, `NetworkMetaData`
    translate remote-side exceptions into `InvalidOperationException`

> **Note on RMI:** the book uses Java RMI for the server-based JDBC implementation.
> Since .NET has no direct RMI equivalent, this repository reproduces the same
> architecture (remote interfaces → server impls → client stubs → JDBC wrappers)
> using `TcpListener` / `TcpClient` and a small line-based text protocol. The class
> names mirror the book's design so the structure is easy to compare.

## Repository Layout

```text
DBSharp/
├── SimpleDB.cs                    # top-level facade (FileMgr+LogMgr+BufferMgr+MetadataMgr+Planner)
├── Buffer/
│   ├── Buffer.cs
│   ├── AbstractBufferMgr.cs
│   ├── BufferMgr.cs
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
├── Record/, Metadata/, Scan/, Predicate/, Planner/   # higher-level layers
├── Jdbc/
│   ├── IDriver.cs                 # core JDBC-style interfaces
│   ├── IConnection.cs
│   ├── IStatement.cs
│   ├── IResultSet.cs
│   ├── IResultSetMetaData.cs
│   ├── DriverAdapter.cs           # default-throwing adapter bases
│   ├── ConnectionAdapter.cs
│   ├── StatementAdapter.cs
│   ├── ResultSetAdapter.cs
│   ├── ResultSetMetaDataAdapter.cs
│   ├── Embedded/
│   │   ├── EmbeddedDriver.cs
│   │   ├── EmbeddedConnection.cs
│   │   ├── EmbeddedStatement.cs
│   │   ├── EmbeddedResultSet.cs
│   │   └── EmbeddedMetaData.cs
│   └── Network/
│       ├── IRemoteDriver.cs       # RMI-style "remote" interfaces
│       ├── IRemoteConnection.cs
│       ├── IRemoteStatement.cs
│       ├── IRemoteResultSet.cs
│       ├── IRemoteMetaData.cs
│       ├── RemoteDriverImpl.cs    # server-side implementations
│       ├── RemoteConnectionImpl.cs
│       ├── RemoteStatementImpl.cs
│       ├── RemoteResultSetImpl.cs
│       ├── RemoteMetaDataImpl.cs
│       ├── TcpSession.cs          # client-side TCP session shared by stubs
│       ├── SimpleDbServer.cs      # TCP listener (default port 1099)
│       ├── RemoteConnectionStub.cs# client-side proxies
│       ├── RemoteStatementStub.cs
│       ├── RemoteResultSetStub.cs
│       ├── RemoteMetaDataStub.cs
│       ├── NetworkDriver.cs       # JDBC client wrappers
│       ├── NetworkConnection.cs
│       ├── NetworkStatement.cs
│       ├── NetworkResultSet.cs
│       └── NetworkMetaData.cs
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

## Quick Usage Example (low-level building blocks)

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

## Embedded JDBC Usage

`EmbeddedDriver` is the easiest way to drive the engine: it sets up a
`SimpleDB` from a directory path and gives back a JDBC-style `IConnection`.

```csharp
using DBSharp.Jdbc;
using DBSharp.Jdbc.Embedded;

IDriver driver = new EmbeddedDriver();
IConnection conn = driver.Connect("studentdb"); // directory path

IStatement stmt = conn.CreateStatement();

// DDL / DML — autocommits on success, rolls back on exception
stmt.ExecuteUpdate("create table student(sname varchar(10), gradyear int)");
stmt.ExecuteUpdate("insert into student(sname, gradyear) values('alice', 2024)");
stmt.ExecuteUpdate("insert into student(sname, gradyear) values('bob', 2025)");

// Queries
IResultSet rs = stmt.ExecuteQuery("select sname, gradyear from student");
IResultSetMetaData md = rs.GetMetaData();
for (int i = 1; i <= md.GetColumnCount(); i++)
    Console.Write(md.GetColumnName(i) + "\t");
Console.WriteLine();

while (rs.Next())
    Console.WriteLine($"{rs.GetString("sname")}\t{rs.GetInt("gradyear")}");

rs.Close();   // commits the surrounding read transaction
conn.Close(); // commits and releases the connection
```

Behavior to be aware of:

- `ExecuteUpdate` commits on success and rolls back on exception.
- `ExecuteQuery` keeps the transaction open; closing the `IResultSet` (or the
  `IConnection`) commits it.
- A connection always has a "current transaction" — after every commit/rollback
  a fresh one is started so the connection stays usable.

## Network JDBC Usage (RMI-style, TCP-based)

To run as a server, instantiate `SimpleDB` and hand it to `SimpleDbServer`:

```csharp
using DBSharp;
using DBSharp.Jdbc.Network;

var db = new SimpleDB("studentdb");           // open or create the database
var server = new SimpleDbServer(db, port: 1099);

// blocks; runs the accept loop on this thread (use a Thread if you need otherwise)
server.Start();
```

From a client process, point a `NetworkDriver` at the host:

```csharp
using DBSharp.Jdbc;
using DBSharp.Jdbc.Network;

IDriver driver = new NetworkDriver(port: 1099);
IConnection conn = driver.Connect("localhost");   // host (or IP) of the server
IStatement stmt = conn.CreateStatement();

stmt.ExecuteUpdate("create table emp(name varchar(15), salary int)");
stmt.ExecuteUpdate("insert into emp(name, salary) values('dave', 50000)");

IResultSet rs = stmt.ExecuteQuery("select name, salary from emp");
while (rs.Next())
    Console.WriteLine($"{rs.GetString("name")}\t{rs.GetInt("salary")}");

rs.Close();
conn.Close();
```

The wire protocol is intentionally simple: one command per line, replies are
either `OK`/`OK <count>`/data lines, or `ERROR <message>` which the client
turns back into an `InvalidOperationException`.

| Command            | Meaning                                                   |
| ------------------ | --------------------------------------------------------- |
| `QUERY <sql>`      | execute a query; opens a server-side result set           |
| `UPDATE <sql>`     | execute an update/DDL; reply: `OK <affected>`             |
| `NEXT`             | advance the active result set; reply: `true` / `false`    |
| `GETINT <field>`   | read int from current row                                 |
| `GETSTRING <fld>`  | read string from current row                              |
| `METADATA`         | reply: count line, then `<name>\t<type>\t<displaySize>`×N |
| `CLOSERS`          | close the active result set                               |
| `CLOSE`            | close the connection (auto-commits)                       |

## Notes

- API design is still evolving.
- This project is intended for learning and incremental DB engine development.
- Chapter 11 (JDBC Interfaces) of *Database Design and Implementation* (Sciore)
  is implemented here. The book's RMI-based server is replaced by an equivalent
  TCP-based design because .NET does not ship with RMI.

## Japanese README

Japanese version: [README.ja.md](./README.ja.md)
