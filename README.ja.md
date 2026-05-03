# DBSharp

DBSharp は C#（.NET 8）で実装している、学習目的のデータベースエンジンです。
ストレージ層の基礎からトランザクション処理、さらに JDBC 互換のクライアント API
（埋め込み版・ネットワーク版）まで段階的に実装しています。

- 固定長ブロック単位のファイル I/O
- ページ内の基本型シリアライズ
- 追記型ログと順方向・逆方向の走査
- 複数の置換ポリシーに対応したバッファプールの pin/unpin 管理
- WAL ベースのリカバリ（undo-only および undo-redo）
- 共有/排他ロックとデッドロック防止（wait-die、wound-wait）による同時実行制御
- 静止型・非静止型チェックポイント
- コミット・ロールバック・クラッシュリカバリ対応のトランザクション
- レコードマネージャ、メタデータカタログ、スキャン、述語、クエリ/更新プランナ
- **JDBC 風のクライアント API**（`DBSharp.Jdbc`）— **埋め込み版**と **ネットワーク版（RMI 相当）**
- **`SimpleDB` ファサードクラス** — FileMgr / LogMgr / BufferMgr / MetadataMgr / Planner を一括で構築

## 現在の実装状況

ローカルテストで確認済み:

- `File.BlockId`
  - ブロック識別子（ファイル名 + ブロック番号）、等価性、ハッシュ、文字列表現
- `File.Page`
  - `int` / `short` / `bool` / `DateTime` / `byte[]` / `string` の読み書き
  - 固定長バイト配列と境界チェック
- `File.FileMgr`
  - ブロック read/write/append/truncate
  - ブロック数ベースのファイル長取得
  - 起動時の `temp` 接頭辞ファイル削除
  - I/O 統計追跡（読み取り・書き込み・追記のブロック数）
- `Log.LogMgr` + `Log.BackwardLogIterator` + `Log.ForwardLogIterator`
  - ログレコードのページ追記
  - LSN ベースの flush 制御
  - 新しいレコードから古いレコードへの反復（逆方向）、古いレコードから新しいレコードへの反復（順方向）
- `Log.LogRecord`
  - 型付きログレコード: Checkpoint, Start, Commit, Rollback, SetInt, SetString, NQCheckpoint, Append
  - ログバイト列から適切なレコード型へのファクトリメソッド
  - レコード型ごとの undo / redo サポート
- `Log.UndoOnlyRecoveryMgr`
  - WAL ベースの undo-only リカバリ（force ポリシー — コミット時にダーティバッファをフラッシュ）
  - ロールバック（単一トランザクションの undo）とリカバー（未コミット全件の undo）
  - SetInt/SetString の旧値ログ
  - 静止型・非静止型チェックポイント両対応
- `Log.UndoRedoRecoveryMgr`
  - WAL ベースの undo-redo リカバリ（no-force ポリシー — コミット時にバッファフラッシュしない）
  - 2 パスリカバリ: undo パス（逆方向）→ redo パス（順方向）
  - SetInt/SetString の旧値・新値ログ
- `Buffer.Buffer` + `Buffer.AbstractBufferMgr` + 置換ポリシーバリエーション
  - pin/unpin ワークフロー
  - 利用可能バッファ数の管理
  - 置換ポリシー:
    - unpinned バッファ選択による基本置換 (`BufferMgr`)
    - unpinned フレーム内の FIFO 退避 (`FIFOBufferMgr`)
    - 最後に unpin されてから最も時間が経ったフレームを退避する LRU (`LRUBufferMgr`)
    - Clock（セカンドチャンス）スイープ (`ClockBufferMgr`)
    - クリーン優先・ダーティフォールバック (`CleanFirstBufferMgr`)
    - LSN ベース: クリーン優先、ダーティでは最小 LSN を選択 (`LSNBasedBufferMgr`)
  - ハッシュテーブルベースのバッファ検索 (`BufferMgrWithBufferHashTable`)
  - 枯渇時タイムアウトと `BufferAbortException`
- `Concurrency.ConcurrencyMgr` + `Concurrency.ILockTable`
  - 共有 (S) ロックと排他 (X) ロックのプロトコル
  - S ロックから X ロックへのロックエスカレーション
  - `ILockTable` によるプラガブルなデッドロック防止:
    - wait-die プロトコル (`WaitDieLockTable`)
    - wound-wait プロトコル (`WoundWaitLockTable`)
  - コンフリクト時の `LockAbortException`
- `Checkpoint.ICheckpointStrategy`
  - 静止型チェックポイント: 新規トランザクションをブロックし、実行中のトランザクション完了を待機 (`QuiescentCheckpointStrategy`)
  - 非静止型チェックポイント: ブロックせずにアクティブなトランザクションのスナップショットを取得 (`NonQuiescentCheckpointStrategy`)
- `Transaction.Transaction` + `Transaction.BufferList`
  - int / string 値のトランザクショナルな読み書き
  - コミット、ロールバック、クラッシュリカバリ
  - トランザクション単位のバッファ pin 管理
  - 同時実行制御下でのブロック append / truncate / size 操作
  - ファントム防止のための EOF センチネルロック
  - リカバリ戦略の選択（undo-only または undo-redo）
- `SimpleDB`（最上位ファサード）
  - FileMgr / LogMgr / BufferMgr / MetadataMgr / Planner を 1 つのコンストラクタで構築
  - 既存 DB を開いた場合は自動でクラッシュリカバリを実行
  - トランザクションファクトリ (`NewTx()`)
- `Jdbc`（JDBC 風 API の表面）
  - コアインターフェース: `IDriver` / `IConnection` / `IStatement` / `IResultSet` / `IResultSetMetaData`
  - 既定で例外を投げるアダプタ基底クラス:
    `DriverAdapter` / `ConnectionAdapter` / `StatementAdapter` / `ResultSetAdapter` / `ResultSetMetaDataAdapter`
- `Jdbc.Embedded`（同一プロセス埋め込み版）
  - `EmbeddedDriver` がディレクトリパスから `SimpleDB` を起動
  - `EmbeddedConnection` は現在の `Transaction` を保持し、`Close()` で自動コミット
  - `EmbeddedStatement` は `Planner.ExecuteUpdate` / `CreateQueryPlan` を呼び、
    成功時はコミット、例外時はロールバック
  - `EmbeddedResultSet` は `IScan` と `Schema` をラップし、`Close()` で読み取り tx をコミット
  - `EmbeddedMetaData` は列数・列名・SQL 型・表示幅を返す
- `Jdbc.Network`（ネットワーク版 / RMI 相当・TCP ベース）
  - サーバ側: `SimpleDbServer`（TCP リスナ、既定ポート `1099`）、
    `RemoteConnectionImpl` / `RemoteStatementImpl` / `RemoteResultSetImpl` / `RemoteMetaDataImpl`
  - プロトコル: 行ベースのテキストプロトコル（`QUERY` / `UPDATE` / `NEXT` /
    `GETINT` / `GETSTRING` / `METADATA` / `CLOSERS` / `CLOSE`）。エラーは
    `ERROR <メッセージ>` 行で返却
  - クライアント側: `NetworkDriver` と
    `RemoteConnectionStub` / `RemoteStatementStub` / `RemoteResultSetStub` / `RemoteMetaDataStub`
    が同じインターフェースを TCP 越しにプロキシ
  - クライアントラッパ: `NetworkConnection` / `NetworkStatement` /
    `NetworkResultSet` / `NetworkMetaData` がリモート側の例外を
    `InvalidOperationException` に変換

> **RMI について:** 教科書ではサーバ版 JDBC に Java RMI を使っていますが、
> .NET には RMI に相当する標準機構がないため、本リポジトリでは
> 同じアーキテクチャ（リモートインターフェース → サーバ実装 → クライアント
> スタブ → JDBC ラッパ）を `TcpListener` / `TcpClient` と簡易な行ベース
> プロトコルで再現しています。クラス名は教科書の設計を踏襲しているので、
> 比較しやすいはずです。

## ディレクトリ構成

```text
DBSharp/
├── SimpleDB.cs                    # 最上位ファサード (FileMgr+LogMgr+BufferMgr+MetadataMgr+Planner)
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
├── Record/, Metadata/, Scan/, Predicate/, Planner/   # 上位レイヤ
├── Jdbc/
│   ├── IDriver.cs                 # JDBC 風コアインターフェース
│   ├── IConnection.cs
│   ├── IStatement.cs
│   ├── IResultSet.cs
│   ├── IResultSetMetaData.cs
│   ├── DriverAdapter.cs           # 既定で例外を投げるアダプタ基底
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
│       ├── IRemoteDriver.cs       # RMI 相当の "remote" インターフェース
│       ├── IRemoteConnection.cs
│       ├── IRemoteStatement.cs
│       ├── IRemoteResultSet.cs
│       ├── IRemoteMetaData.cs
│       ├── RemoteDriverImpl.cs    # サーバ側実装
│       ├── RemoteConnectionImpl.cs
│       ├── RemoteStatementImpl.cs
│       ├── RemoteResultSetImpl.cs
│       ├── RemoteMetaDataImpl.cs
│       ├── TcpSession.cs          # スタブが共有するクライアント TCP セッション
│       ├── SimpleDbServer.cs      # TCP リスナ (既定ポート 1099)
│       ├── RemoteConnectionStub.cs# クライアント側プロキシ
│       ├── RemoteStatementStub.cs
│       ├── RemoteResultSetStub.cs
│       ├── RemoteMetaDataStub.cs
│       ├── NetworkDriver.cs       # JDBC クライアントラッパ
│       ├── NetworkConnection.cs
│       ├── NetworkStatement.cs
│       ├── NetworkResultSet.cs
│       └── NetworkMetaData.cs
└── DBSharp.Tests/
    └── Program.cs
```

## 必要環境

- .NET SDK 8.0 以上

## ビルド

```bash
dotnet build
```

## テスト実行

テストプロジェクトは xUnit/NUnit ではなく、コンソールベースの軽量ランナーです。

```bash
dotnet run --project DBSharp.Tests/DBSharp.Tests.csproj
```

## 利用例 (低レベル API)

```csharp
using DBSharp.File;
using DBSharp.Log;
using DBSharp.Buffers;
using DBSharp.Transactions;

var fm = new FileMgr(new DirectoryInfo("mydb"), blocksize: 400);
var lm = new LogMgr(fm, "simpledb.log");
var bm = new BufferMgr(fm, lm, numbuffs: 8);

// トランザクションを使ってデータを読み書き
var tx = new Transaction(fm, lm, bm);
fm.Append("data.tbl");
var blk = new BlockId("data.tbl", 0);

tx.Pin(blk);
tx.SetInt(blk, 0, 123, okToLog: true);
tx.SetString(blk, 4, "hello", okToLog: true);
tx.Commit();
```

## 埋め込み JDBC の利用例

`EmbeddedDriver` を使うのが一番手軽です。ディレクトリパスから `SimpleDB` を
構築し、JDBC 風の `IConnection` を返します。

```csharp
using DBSharp.Jdbc;
using DBSharp.Jdbc.Embedded;

IDriver driver = new EmbeddedDriver();
IConnection conn = driver.Connect("studentdb"); // ディレクトリパス

IStatement stmt = conn.CreateStatement();

// DDL / DML — 成功時にオートコミット、例外時はロールバック
stmt.ExecuteUpdate("create table student(sname varchar(10), gradyear int)");
stmt.ExecuteUpdate("insert into student(sname, gradyear) values('alice', 2024)");
stmt.ExecuteUpdate("insert into student(sname, gradyear) values('bob', 2025)");

// クエリ
IResultSet rs = stmt.ExecuteQuery("select sname, gradyear from student");
IResultSetMetaData md = rs.GetMetaData();
for (int i = 1; i <= md.GetColumnCount(); i++)
    Console.Write(md.GetColumnName(i) + "\t");
Console.WriteLine();

while (rs.Next())
    Console.WriteLine($"{rs.GetString("sname")}\t{rs.GetInt("gradyear")}");

rs.Close();   // 読み取りトランザクションをコミット
conn.Close(); // コミットしてコネクションを解放
```

注意点:

- `ExecuteUpdate` は成功時にコミット、例外時にロールバックします。
- `ExecuteQuery` はトランザクションを開いたままにします。`IResultSet` または
  `IConnection` を `Close()` した時点でコミットされます。
- コネクションは常に「現在のトランザクション」を保持しており、コミット／
  ロールバックのたびに新しいトランザクションを開始するので、コネクション
  オブジェクト自体は使い続けられます。

## ネットワーク JDBC の利用例 (RMI 相当・TCP ベース)

サーバとして動かすには、`SimpleDB` を作って `SimpleDbServer` に渡します。

```csharp
using DBSharp;
using DBSharp.Jdbc.Network;

var db = new SimpleDB("studentdb");           // DB を開くか作成する
var server = new SimpleDbServer(db, port: 1099);

// ブロッキング呼び出し。別スレッドにしたい場合は Thread でラップしてください。
server.Start();
```

クライアントプロセスからは `NetworkDriver` をホスト名に向けるだけです。

```csharp
using DBSharp.Jdbc;
using DBSharp.Jdbc.Network;

IDriver driver = new NetworkDriver(port: 1099);
IConnection conn = driver.Connect("localhost"); // サーバのホスト名 / IP
IStatement stmt = conn.CreateStatement();

stmt.ExecuteUpdate("create table emp(name varchar(15), salary int)");
stmt.ExecuteUpdate("insert into emp(name, salary) values('dave', 50000)");

IResultSet rs = stmt.ExecuteQuery("select name, salary from emp");
while (rs.Next())
    Console.WriteLine($"{rs.GetString("name")}\t{rs.GetInt("salary")}");

rs.Close();
conn.Close();
```

ワイヤープロトコルは意図的にシンプルです。コマンドは 1 行 1 つで、応答は
`OK` / `OK <件数>` / データ行のいずれか、もしくは `ERROR <メッセージ>` 行
（クライアント側で `InvalidOperationException` に変換）です。

| コマンド             | 内容                                                          |
| ------------------- | ------------------------------------------------------------ |
| `QUERY <sql>`       | クエリ実行。サーバ側に結果セットを開く                         |
| `UPDATE <sql>`      | 更新 / DDL 実行。応答は `OK <影響行数>`                       |
| `NEXT`              | 結果セットを次の行へ。応答は `true` / `false`                |
| `GETINT <field>`    | 現在行から int を取得                                         |
| `GETSTRING <field>` | 現在行から string を取得                                      |
| `METADATA`          | 列数行 + 各列 `<名前>\t<型>\t<表示幅>` を N 行返す           |
| `CLOSERS`           | 現在の結果セットをクローズ                                    |
| `CLOSE`             | コネクションをクローズ（自動コミット）                        |

## 補足

- API は今後変更される可能性があります。
- 学習・段階的実装を前提としたリポジトリです。
- 教科書 *Database Design and Implementation* (Sciore) の第 11 章「JDBC
  Interfaces」に対応する実装です。教科書の RMI ベースのサーバは、.NET に
  RMI が無いため等価な TCP ベースの設計に置き換えています。

## English README

English version: [README.md](./README.md)
