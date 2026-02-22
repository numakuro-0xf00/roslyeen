# 設計レビュー指摘事項

## 1. 並行性・スレッドセーフティ

### 1.1 SolutionManager の同期ロック (Critical)

`SolutionManager.GetSolution()` (L76), `FindDocument()` (L144), `GetProjects()` (L181) が `_lock.Wait()` で同期ブロックしている。これらは `QueryExecutor` の `async` メソッドから呼ばれるため、スレッドプール枯渇やデッドロックのリスクがある。

```
GetSolution() -> _lock.Wait()  // 同期ブロック
  ↑ 呼び出し元: QueryExecutor.GetDefinitionAsync() 等（全て async）
```

さらに深刻な問題として、`GetSolution()` は `Solution` 参照を返した後にロックを解放するが、呼び出し元がその `Solution` を使用中に `LoadSolutionAsync()` が走ると、旧 `_workspace` が Dispose され、返却済みの `Solution` 参照が無効化される可能性がある。

### 1.2 IpcServer のクライアントタスクリスト肥大化

`_clientTasks` にタスクが追加されるのみで、完了タスクは削除されない（`AcceptClientsAsync` L79-84）。長時間稼働で Task オブジェクトがリークする。

### 1.3 DaemonHost.OnFilesChanged が async void

`async void` はカレントの try-catch で例外を捕捉しているが、複数のファイル変更が同時に発生した場合の順序制御が不可能。呼び出し元が await できないため、リロード完了を待たずに次のクエリが旧 Solution に対して実行される可能性がある。

## 2. IPC プロトコル

### 2.1 エンディアン非指定

`BitConverter.ToInt32` / `BitConverter.GetBytes` はプラットフォームのネイティブバイトオーダーを使用する。現時点では同一マシン上のクライアント・サーバー通信なので問題ないが、プロトコルとしてはエンディアンを明示すべき（`BinaryPrimitives.ReadInt32LittleEndian` 等）。

### 2.2 リクエストタイムアウト未実装

`HandleClientAsync` にリクエスト単位のタイムアウトがない。大規模ソリューションの全体 diagnostics 等で長時間ブロックする可能性がある。

### 2.3 プロトコルバージョン交渉なし

クライアント・サーバー間のバージョン不一致を検出する手段がない。CLI とデーモンのバージョンが異なる場合に無言で失敗する。

### 2.4 QueryResult と JSON-RPC エラーの二重レイヤー

`IpcServer.HandleRequestAsync` で、symbol-not-found を JSON-RPC エラー (`-32001`) にマッピングしている。しかし「シンボルが見つからない」はプロトコルエラーではなく正常な応答結果。JSON-RPC の `result` に `QueryResult(Success=false)` を入れて返すべき。現状では、プロトコルエラーとアプリケーションレベルの「結果なし」が混在している。

## 3. リソース管理

### 3.1 Solution リロード時のライフサイクル問題

`SolutionManager.LoadSolutionAsync()` (L55) で旧 `_workspace` を Dispose してから新 workspace を作成するが、既に返却済みの `Solution` オブジェクトを使ったクエリが進行中の場合、そのクエリは不整合な状態で実行される。

提案: `ReaderWriterLockSlim` または copy-on-write パターンで、読み取り中のクエリを安全に完了させてからリロードする。

### 3.2 DaemonManager.GetDaemonPath() の脆弱な探索

3つのハードコードされた相対パスで Daemon DLL を探索している。ビルド構成やインストール方法が変わると無言で失敗する。

## 4. エラーハンドリング

### 4.1 サイレント catch の多用

以下の箇所で例外が無視されており、問題の診断が困難:

| 箇所 | ファイル:行 |
|------|------------|
| Dispose 時のファイル削除 | `DaemonHost.cs:178-183` |
| クライアント受付ループ | `IpcServer.cs:91-94` |
| クライアント処理 | `IpcServer.cs:152-155` |
| イベント発火 | `DebouncedFileWatcher.cs:163-166` |
| Watcher エラー | `DebouncedFileWatcher.cs:110-113` |

### 4.2 構造化ログの欠如

全ての診断出力が `Console.Error.WriteLine` 直書き。デーモンプロセスの stderr はクライアントから参照不可能な場合がある。ログレベル、フィルタリング、構造化フォーマットが一切ない。

## 5. 設計・アーキテクチャ

### 5.1 CLI コマンドの大量重複

`NavigationCommands.cs` (3クラス) と `RelationshipCommands.cs` (6クラス) が以下のパターンをほぼ同一に繰り返している:

1. `FileArg`, `LineArg`, `ColumnArg` 定義
2. `SetAction` でパース → `ExecuteAsync` 呼び出し
3. `ExecuteAsync` で `ResolveSolutionPath` → `GetOrStartDaemonAsync` → `SendRequestAsync` → `FormatXxx`

ジェネリックな基底クラスで ~80% の重複を排除可能。

### 5.2 FindDocument の O(n) スキャン

`SolutionManager.FindDocument()` と `TestSolutionProvider.FindDocument()` は全プロジェクトの全ドキュメントを線形探索する。大規模ソリューションでは全クエリリクエストでこのコストが発生する。`Dictionary<string, DocumentId>` のインデックスを構築すべき。

### 5.3 XML ドキュメント解析のフラジャイルな実装

`QueryExecutor.GetSymbolInfoAsync()` (L296-301) で `IndexOf("summary")` / `Substring` による手動 XML パースを行っている。複数行 summary、XML エンティティ、ネストされた要素で壊れる。`XDocument` や `XmlReader` を使うべき。

### 5.4 ソケットパスのハッシュ衝突リスク

`PathResolver.ComputeShortHash` は SHA-256 の先頭 8 バイト (64bit) を使用。衝突確率は極めて低いが、より実用的な問題として、パスの正規化がプラットフォーム依存であるため、同じソリューションを異なるパス形式で指定すると異なるハッシュが生成され、既存デーモンに接続できない。

## 6. ファイル変更追跡の不備

### 6.1 .cs ファイル削除が未処理

`DaemonHost.OnFilesChanged` で `FileChangeType.Deleted` の .cs ファイルは `continue` でスキップされ、コメントで「次のフルリロードで処理」とある。削除されたファイルのシンボルがインメモリ Solution に残存する。

### 6.2 新規 .cs ファイルの追加が不可能

新規 .cs ファイルが作成された場合、`UpdateDocumentFromDiskAsync` が呼ばれるが、そのドキュメントは Solution に存在しないため `FindDocumentId` が null を返し、何も起きない。新規ファイルはフルリロードまで認識されない。

## 7. 未実装の設計要素

CLAUDE.md で設計されているが未実装の機能:

| カテゴリ | コマンド | 状態 |
|---------|---------|------|
| Structure | `signature`, `members`, `hierarchy`, `overview` | 未実装 |
| Relationships | `dependencies` | 未実装 |
| Diagnostics | `unused`, `type-check` | 未実装 |
| Global Option | `--project, -p` | 未実装 |
| Global Option | `--absolute-paths` | 未実装 |

## 8. テストカバレッジ

### 8.1 テストされていない領域

- デーモンライフサイクル (`DaemonHost`, `IpcServer`, `IpcClient` の結合)
- `DebouncedFileWatcher` (デバウンスタイミング、イベント合成、フルリロード判定)
- `DaemonManager` (起動リトライ、接続タイムアウト、クリーンアップ)
- 管理コマンド (`init`, `status`, `shutdown`)
- CLI 引数パースのエッジケース
- エラーパス (接続失敗、デーモンクラッシュ)
- `SolutionManager` への並行アクセス

### 8.2 E2E テストがプレースホルダー

`EndToEndTests.Placeholder_EndToEndTestInfrastructureNotYetImplemented` がスキップ状態。実際のプロセス起動・ソケット通信の E2E テストが存在しない。

## 9. セキュリティ

### 9.1 Unix ソケットの認証なし

ソケットファイルにアクセス可能な全ローカルユーザーがデーモンに接続可能。マルチユーザー環境で他ユーザーのデーモンにクエリ送信が可能。ソケットファイルのパーミッション (`chmod 600`) を明示的に設定すべき。

## 10. 優先度まとめ

| 優先度 | 項目 | 理由 |
|--------|------|------|
| P0 | 1.1 同期ロック問題 | デッドロックのリスク |
| P0 | 3.1 リロード時のライフサイクル | クエリ中のクラッシュリスク |
| P1 | 2.4 エラーレイヤーの混在 | CLI の終了コードに影響 |
| P1 | 6.1-6.2 ファイル変更追跡 | コア機能の不完全性 |
| P1 | 4.2 ログ基盤の欠如 | デバッグ不能 |
| P2 | 5.1 コマンド重複 | 保守性 |
| P2 | 5.2 FindDocument O(n) | 大規模ソリューションのパフォーマンス |
| P2 | 1.2 タスクリスト肥大化 | 長時間稼働でのメモリリーク |
| P3 | 7 未実装機能 | 機能完全性 |
| P3 | 8 テストカバレッジ | 品質保証 |
