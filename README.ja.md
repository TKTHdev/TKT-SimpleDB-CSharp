# DBSharp

DBSharp は C#（.NET 8）で実装している、学習目的のデータベースエンジンです。
現時点ではストレージ層の基礎機能を中心に実装しています。

- 固定長ブロック単位のファイル I/O
- ページ内の基本型シリアライズ
- 追記型ログと新しい順での走査
- バッファプールの pin/unpin 管理

まだフル機能の DBMS ではなく、基盤コンポーネントの段階です。

## 現在の実装状況

ローカルテスト（21件）で確認済み:

- `File.BlockId`
  - ブロック識別子（ファイル名 + ブロック番号）、等価性、ハッシュ、文字列表現
- `File.Page`
  - `int` / `short` / `bool` / `DateTime` / `byte[]` / `string` の読み書き
  - 固定長バイト配列と境界チェック
- `File.FileMgr`
  - ブロック read/write/append
  - ブロック数ベースのファイル長取得
  - 起動時の `temp` 接頭辞ファイル削除
- `Log.LogMgr` + `Log.LogIterator`
  - ログレコードのページ追記
  - LSN ベースの flush 制御
  - 新しいレコードから古いレコードへの反復
- `Buffer.Buffer` + `Buffer.BufferMgr`
  - pin/unpin ワークフロー
  - 利用可能バッファ数の管理
  - unpinned バッファ選択による基本置換
  - 枯渇時タイムアウトと `BufferAbortException`

## ディレクトリ構成

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
using DBSharp.Buffer;

var fm = new FileMgr(new DirectoryInfo("mydb"), blocksize: 400);
var lm = new LogMgr(fm, "simpledb.log");
var bm = new BufferMgr(fm, lm, numbuffs: 8);

// 1ブロック確保
fm.Append("data.tbl");
var blk = new BlockId("data.tbl", 0);

// バッファを pin して更新し、unpin
var buff = bm.Pin(blk);
buff.Contents().SetInt(0, 123);
buff.SetModified(txnum: 1, lsn: lm.Append(new byte[] { 1, 2, 3 }));
bm.Unpin(buff);
```

## 補足

- API は今後変更される可能性があります。
- 並行制御などはまだ基礎段階です。
- 学習・段階的実装を前提としたリポジトリです。

## English README

English version: [README.md](./README.md)
