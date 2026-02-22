---
name: roslyn-query
description: C# コードナビゲーション CLI ツール roslyn-query を使って、定義ジャンプ、参照検索、コール階層、シンボル情報取得、コンパイル診断などを実行する。C# ソリューション内のシンボルを調査・ナビゲーションする必要があるとき、メソッドの呼び出し元/呼び出し先を知りたいとき、インターフェースの実装を探したいとき、コンパイルエラーを確認したいときに使用する。
---

# roslyn-query

Roslyn ベースの C# コードナビゲーション CLI。デーモンアーキテクチャでソリューションをメモリに保持し、IDE レベルのコード解析をコマンドラインから実行する。

## セットアップ

```bash
# グローバルインストール
dotnet pack src/RoslynQuery.Cli -c Release -o ./nupkg
dotnet tool install --global --add-source ./nupkg RoslynQuery
```

## 基本ワークフロー

```bash
# コマンドを直接実行（デーモンは自動起動される）
roslyn-query <command> <file> <line> <column> [options]

# 不要になったら停止
roslyn-query shutdown
```

**`init` は不要** -- 任意のクエリコマンドがデーモンを自動起動する。`init` は事前ウォームアップ用のオプションコマンド。

`--json` を付けると全コマンドで JSON 出力になる。ファイルパスはソリューションルート相対、座標は 1-based。

## コマンド早見表

| 目的 | コマンド | 引数 |
|------|---------|------|
| 定義ジャンプ | `definition <file> <line> <col>` | |
| 基底定義ジャンプ | `base-definition <file> <line> <col>` | |
| 実装検索 | `implementations <file> <line> <col>` | |
| 参照検索 | `references <file> <line> <col>` | `--include-definition` |
| 呼び出し元 | `callers <file> <line> <col>` | |
| 呼び出し先 | `callees <file> <line> <col>` | |
| シンボル詳細 | `symbol <file> <line> <col>` | |
| コンパイル診断 | `diagnostics [file]` | `--warnings` `--info` |
| デーモン事前起動（任意） | `init` | `-s <sln>` |
| 状態確認 | `status` | |
| デーモン停止 | `shutdown` | |

## 使い方ガイド

### デーモン管理

デーモンは任意のクエリコマンドで自動起動されるため、手動で `init` を呼ぶ必要はない。初回コマンド実行時に自動的にデーモンが起動し、以降はメモリに保持される。

```bash
# いきなりクエリを実行できる（デーモンは自動起動）
roslyn-query definition src/Foo.cs 42 15
# stderr: Starting daemon for MyProject.sln...
# => src/Bar.cs:10:5

# 状態確認
roslyn-query status
```

### シンボル調査

あるシンボルについて知りたいとき、まず `symbol` で概要を把握し、`definition` で定義に飛び、`references` で使用箇所を確認する。

```bash
# シンボルの詳細を確認
roslyn-query symbol src/Services/UserService.cs 30 15

# 定義位置へジャンプ
roslyn-query definition src/Controllers/UserController.cs 22 20

# 参照箇所を列挙
roslyn-query references src/Services/UserService.cs 30 15
```

### 継承・実装の追跡

インターフェースの実装や基底クラスのオーバーライドを追跡するとき。

```bash
# インターフェースメソッドの全実装を検索
roslyn-query implementations src/IRepository.cs 12 10

# オーバーライドメソッドの元定義（インターフェース/基底クラス）へジャンプ
roslyn-query base-definition src/SqlRepository.cs 45 15
```

### コール階層の探索

メソッドの依存関係を把握したいとき。

```bash
# このメソッドを呼んでいる箇所
roslyn-query callers src/Services/OrderService.cs 50 15

# このメソッドが内部で呼んでいるメソッド
roslyn-query callees src/Services/OrderService.cs 50 15
```

### コンパイル診断

```bash
# 特定ファイルのエラー・警告
roslyn-query diagnostics src/Foo.cs

# ソリューション全体
roslyn-query diagnostics

# Info レベルも含める
roslyn-query diagnostics --info
```

## 注意事項

- 終了コード: 0=成功, 1=シンボル未検出, 4=接続失敗
- `.cs` ファイル変更はデーモンが自動検知して差分更新する（10-200ms）
- `.csproj` / `.sln` 変更時はフルリロード（3-15s）
- 詳細なコマンドリファレンスは [references/api_reference.md](references/api_reference.md) を参照

## 他プロジェクトで使う

このスキルを別の C# プロジェクトで使うには、`.claude/skills/roslyn-query/` ディレクトリごとコピーしてください。

```bash
cp -r <roslyeen-repo>/.claude/skills/roslyn-query <your-project>/.claude/skills/
```
