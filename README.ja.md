# DBSharp

DBSharp は C#（.NET 8）で実装している、学習目的のデータベースエンジンです。
ストレージ層の基礎からトランザクション処理までを段階的に実装しています。

- 固定長ブロック単位のファイル I/O
- ページ内の基本型シリアライズ
- 追記型ログと順方向・逆方向の走査
- 複数の置換ポリシーに対応したバッファプールの pin/unpin 管理
- WAL ベースのリカバリ（undo-only および undo-redo）
- 共有/排他ロックとデッドロック防止（wait-die、wound-wait）による同時実行制御
- 静止型・非静止型チェックポイント
- コミット・ロールバック・クラッシュリカバリ対応のトランザクション

まだフル機能の DBMS ではなく、ストレージからトランザクション管理までの基盤コンポーネントの段階です。

## 現在の実装状況

ローカルテスト（76件）で確認済み:

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

## ディレクトリ構成

```text
DBSharp/
├── Buffer/
│   ├── Buffer.cs
│   ├── AbstractBufferMgr.cs      # 置換ポリシーのテンプレートメソッド基底クラス
│   ├── BufferMgr.cs              # 素朴な置換
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

## 利用例

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

## 補足

- API は今後変更される可能性があります。
- 学習・段階的実装を前提としたリポジトリです。

## English README

English version: [README.md](./README.md)
