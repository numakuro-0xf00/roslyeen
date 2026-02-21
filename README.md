# roslyeen

Roslyn ベースの C# コードナビゲーション CLI ツール。CLI 上で動作する AI エージェントに IDE レベルのコード解析機能（定義ジャンプ、参照検索、コール階層など）を提供します。

## 必要環境

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## インストール

```bash
# リポジトリをクローン
git clone https://github.com/your-username/roslyeen.git
cd roslyeen

# パッケージ作成 & グローバルインストール
dotnet pack src/RoslynQuery.Cli -c Release -o ./nupkg
dotnet tool install --global --add-source ./nupkg RoslynQuery
```

### 更新

```bash
dotnet pack src/RoslynQuery.Cli -c Release -o ./nupkg
dotnet tool update --global --add-source ./nupkg RoslynQuery
```

### アンインストール

```bash
dotnet tool uninstall --global RoslynQuery
```

## クイックスタート

```bash
# 1. デーモンを起動（ソリューションをロード）
roslyn-query init -s MyProject.sln

# 2. コードナビゲーションを実行
roslyn-query definition src/Foo.cs 42 15
roslyn-query references src/Foo.cs 42 15
roslyn-query callers src/Foo.cs 42 15

# 3. デーモンを停止
roslyn-query shutdown
```

## グローバルオプション

全コマンド共通で使用できるオプション:

| オプション | 短縮形 | 説明 |
|-----------|--------|------|
| `--solution <path>` | `-s` | ソリューションファイルのパス。省略時は環境変数 `ROSLYN_QUERY_SOLUTION`、またはカレントディレクトリから `.sln` を自動検索 |
| `--json` | | JSON 形式で出力 |
| `--verbose` | `-v` | 詳細ログを stderr に出力 |

## 終了コード

| コード | 意味 |
|--------|------|
| 0 | 成功（結果あり） |
| 1 | シンボルが見つからない / デーモン未起動 |
| 2 | ソリューション/プロジェクトのロード失敗 |
| 3 | 引数エラー |
| 4 | デーモン接続失敗 |

## コマンド一覧

### ナビゲーション

#### `definition` - 定義ジャンプ

シンボルの定義位置へ移動します。

```bash
roslyn-query definition <file> <line> <column>
```

```bash
# 例: src/Foo.cs の 42行目 15列目のシンボルの定義を検索
roslyn-query definition src/Foo.cs 42 15
# => src/Bar.cs:10:5

roslyn-query definition src/Foo.cs 42 15 --json
# => {"symbolName":"MyMethod","symbolKind":"Method","location":{"filePath":"src/Bar.cs","line":10,"column":5}}
```

#### `base-definition` - 基底定義ジャンプ

インターフェースや基底クラスのオリジナル定義まで遡ります。

```bash
roslyn-query base-definition <file> <line> <column>
```

```bash
# 例: オーバーライドメソッドの元のインターフェース定義を検索
roslyn-query base-definition src/FooService.cs 25 10
# => src/IFooService.cs:8:5
```

#### `implementations` - 実装検索

インターフェースや仮想メンバーの全実装を検索します。

```bash
roslyn-query implementations <file> <line> <column>
```

```bash
# 例: インターフェースメソッドの全実装を検索
roslyn-query implementations src/IRepository.cs 12 10
# => src/SqlRepository.cs:45:5
# => src/InMemoryRepository.cs:23:5
```

### リレーションシップ

#### `references` - 参照検索

シンボルの全参照箇所を検索します。

```bash
roslyn-query references <file> <line> <column> [--include-definition]
```

| オプション | 説明 |
|-----------|------|
| `--include-definition` | 定義箇所も結果に含める |

```bash
# 例: メソッドの全参照を検索
roslyn-query references src/Foo.cs 42 15
# => src/Bar.cs:10:20
# => src/Baz.cs:55:8

# 定義箇所も含める
roslyn-query references src/Foo.cs 42 15 --include-definition
```

#### `callers` - 呼び出し元検索

メソッドやプロパティの呼び出し元を検索します（コール階層 - 上流）。

```bash
roslyn-query callers <file> <line> <column>
```

```bash
# 例: GetUser メソッドを呼び出している箇所を検索
roslyn-query callers src/UserService.cs 30 15
# => src/UserController.cs:22:12
# => src/AdminService.cs:45:8
```

#### `callees` - 呼び出し先検索

メソッド内で呼び出されているメソッドやプロパティを検索します（コール階層 - 下流）。

```bash
roslyn-query callees <file> <line> <column>
```

```bash
# 例: ProcessOrder メソッドが内部で呼び出しているメソッドを検索
roslyn-query callees src/OrderService.cs 50 15
# => src/PaymentService.cs:12:5
# => src/InventoryService.cs:30:5
```

#### `symbol` - シンボル詳細

シンボルの詳細情報（型、アクセス修飾子、ドキュメントなど）を表示します。

```bash
roslyn-query symbol <file> <line> <column>
```

```bash
# 例: メソッドの詳細情報を表示
roslyn-query symbol src/UserService.cs 30 15
# => Symbol: GetUser
# => Kind: Method
# => Full Name: MyApp.Services.UserService.GetUser
# => Signature: User GetUser(int userId)
# => Return Type: User
# => Accessibility: public
# => Namespace: MyApp.Services
# => Containing Type: UserService
# => Location: src/UserService.cs:30:15
# => Documentation: Gets a user by their ID.
```

#### `diagnostics` - コンパイル診断

コンパイルエラーや警告を表示します。ファイル指定を省略するとソリューション全体が対象になります。

```bash
roslyn-query diagnostics [file] [--warnings] [--info]
```

| オプション | デフォルト | 説明 |
|-----------|-----------|------|
| `--warnings` | true | 警告を含める |
| `--info` | false | Info レベルの診断を含める |

```bash
# 例: 特定ファイルの診断
roslyn-query diagnostics src/Foo.cs
# => src/Foo.cs:10:5: error CS0103: The name 'x' does not exist in the current context
# => src/Foo.cs:25:12: warning CS0168: The variable 'unused' is declared but never used

# ソリューション全体の診断
roslyn-query diagnostics

# Info レベルも含める
roslyn-query diagnostics src/Foo.cs --info
```

### 管理

#### `init` - デーモン起動

ソリューションをロードしてデーモンプロセスを起動します。

```bash
roslyn-query init -s <solution-path>
```

```bash
roslyn-query init -s MyProject.sln
# => Daemon started for MyProject.sln (PID: 12345)

# 既に起動済みの場合
roslyn-query init -s MyProject.sln
# => Daemon already running for MyProject.sln (PID: 12345)
```

#### `status` - デーモン状態確認

デーモンの稼働状態を確認します。

```bash
roslyn-query status
```

```bash
roslyn-query status
# => Solution: MyProject.sln
# => Socket: /tmp/roslyn-query/MyProject.sock
# => PID File: /tmp/roslyn-query/MyProject.pid
# => Running: Yes
# => Process ID: 12345
# => Responsive: Yes

roslyn-query status --json
# => {"solutionPath":"MyProject.sln","socketPath":"/tmp/...","isRunning":true,...}
```

#### `shutdown` - デーモン停止

デーモンプロセスを停止します。

```bash
roslyn-query shutdown
```

```bash
roslyn-query shutdown
# => Daemon stopped

# 既に停止済みの場合
roslyn-query shutdown
# => Daemon is not running
```

## 出力形式

デフォルトではパイプやスクリプトに適したテキスト形式で出力します。`--json` オプションで JSON 形式に切り替えできます。

ファイルパスはソリューションルートからの相対パスで出力されます。座標は 1-based（行・列ともに 1 始まり）です。

## ライセンス

MIT
