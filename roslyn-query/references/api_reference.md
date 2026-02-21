# roslyn-query コマンドリファレンス

## グローバルオプション

| オプション | 短縮形 | 説明 |
|-----------|--------|------|
| `--solution <path>` | `-s` | ソリューションファイルパス。省略時は `ROSLYN_QUERY_SOLUTION` 環境変数 → カレントディレクトリの `.sln` 自動検索 |
| `--json` | | JSON 形式で出力 |
| `--verbose` | `-v` | 詳細ログを stderr に出力 |

## 終了コード

| コード | 意味 |
|--------|------|
| 0 | 成功 |
| 1 | シンボル未検出 / デーモン未起動 |
| 2 | ソリューションロード失敗 |
| 3 | 引数エラー |
| 4 | デーモン接続失敗 |

## ナビゲーションコマンド

### definition

シンボルの定義位置を返す。

```
roslyn-query definition <file> <line> <column>
```

出力: `FilePath:Line:Column`

### base-definition

インターフェースや基底クラスのオリジナル定義まで遡る。

```
roslyn-query base-definition <file> <line> <column>
```

出力: `FilePath:Line:Column`

### implementations

インターフェースや仮想メンバーの全実装を返す。

```
roslyn-query implementations <file> <line> <column>
```

出力: 1行1ロケーション `FilePath:Line:Column`

## リレーションシップコマンド

### references

シンボルの全参照箇所を返す。

```
roslyn-query references <file> <line> <column> [--include-definition]
```

- `--include-definition`: 定義箇所も結果に含める

出力: 1行1ロケーション `FilePath:Line:Column`

### callers

メソッド/プロパティの呼び出し元を返す（コール階層 上流）。

```
roslyn-query callers <file> <line> <column>
```

出力: 1行1ロケーション `FilePath:Line:Column`

### callees

メソッド内で呼び出されているメソッド/プロパティを返す（コール階層 下流）。

```
roslyn-query callees <file> <line> <column>
```

出力: 1行1ロケーション `FilePath:Line:Column`

### symbol

シンボルの詳細情報を返す。

```
roslyn-query symbol <file> <line> <column>
```

テキスト出力例:
```
Symbol: GetUser
Kind: Method
Full Name: MyApp.Services.UserService.GetUser
Signature: User GetUser(int userId)
Return Type: User
Accessibility: public
Modifiers: virtual
Namespace: MyApp.Services
Containing Type: UserService
Location: src/UserService.cs:30:15
Documentation: Gets a user by their ID.
```

### diagnostics

コンパイルエラー/警告を返す。ファイル省略でソリューション全体。

```
roslyn-query diagnostics [file] [--warnings] [--info]
```

- `--warnings` (デフォルト true): 警告を含める
- `--info`: Info レベルも含める

出力: `FilePath:Line:Column: severity ID: Message`

## 管理コマンド

### init

デーモンを起動してソリューションをロードする。

```
roslyn-query init -s <solution-path>
```

出力: `Daemon started for <path> (PID: <pid>)` / `Daemon already running for <path> (PID: <pid>)`

### status

デーモンの稼働状態を確認する。

```
roslyn-query status
```

出力:
```
Solution: MyProject.sln
Socket: /tmp/roslyn-query/MyProject.sock
PID File: /tmp/roslyn-query/MyProject.pid
Running: Yes
Process ID: 12345
Responsive: Yes
```

### shutdown

デーモンを停止する。

```
roslyn-query shutdown
```

出力: `Daemon stopped` / `Daemon is not running`
