# ReaderWriterLockSlim と async/await の非互換性

## 概要

`ReaderWriterLockSlim` を `async/await` と組み合わせて使用すると、`await` 後のスレッド切り替えにより `SynchronizationLockException`（"The write lock is being released without being held."）が発生する。

## 問題のコード

```csharp
private readonly ReaderWriterLockSlim _lock = new();

public async Task LoadSolutionAsync(CancellationToken cancellationToken = default)
{
    _lock.EnterWriteLock();   // スレッド A でロック取得
    try
    {
        // await でスレッドが切り替わる可能性がある
        _solution = await _workspace.OpenSolutionAsync(_solutionPath, cancellationToken);
    }
    finally
    {
        _lock.ExitWriteLock(); // スレッド B で実行 → 例外
    }
}
```

## 原因

`ReaderWriterLockSlim` はスレッドアフィニティ（thread-affine）を持つ。ロックの取得と解放は同一スレッドで行う必要がある。

```
Timeline:
1. Thread A: EnterWriteLock()  → ロック取得成功
2. Thread A: OpenSolutionAsync() を開始、await で中断
3. Thread B: await から再開（スレッドプールの別スレッド）
4. Thread B: ExitWriteLock()   → Thread B はロックを保持していない → 例外
```

`await` はデフォルトで `SynchronizationContext` を使ってコールバックをスケジュールする。コンソールアプリや ASP.NET Core には `SynchronizationContext` がないため、`await` 後の継続はスレッドプールの任意のスレッドで実行される。

## 解決策

### SemaphoreSlim に置き換える

`SemaphoreSlim` はスレッドアフィニティを持たず、`WaitAsync` で async 対応のロック取得ができる。

```csharp
private readonly SemaphoreSlim _lock = new(1, 1);

public async Task LoadSolutionAsync(CancellationToken cancellationToken = default)
{
    await _lock.WaitAsync(cancellationToken);  // async 対応
    try
    {
        _solution = await _workspace.OpenSolutionAsync(_solutionPath, cancellationToken);
    }
    finally
    {
        _lock.Release();  // どのスレッドからでも解放可能
    }
}
```

同期的な読み取り操作には `Wait()` を使用:

```csharp
public Solution GetSolution()
{
    _lock.Wait();
    try
    {
        return _solution ?? throw new InvalidOperationException("Solution not loaded");
    }
    finally
    {
        _lock.Release();
    }
}
```

## トレードオフ

| 項目 | ReaderWriterLockSlim | SemaphoreSlim(1,1) |
|------|---------------------|-------------------|
| async/await 対応 | 不可 | `WaitAsync` で対応 |
| スレッドアフィニティ | あり（同一スレッド必須） | なし |
| Reader/Writer 分離 | 複数 Reader 並行可能 | 排他のみ（1つずつ） |
| 再帰ロック | `LockRecursionPolicy` で対応 | 不可 |

`SemaphoreSlim(1,1)` は Reader/Writer の分離ができないため、読み取り同士も排他になる。読み取り頻度が非常に高く並行性が重要な場合は、`Nito.AsyncEx` の `AsyncReaderWriterLock` 等の外部ライブラリを検討する。

## 判断基準

- **async メソッド内でロックが必要** → `SemaphoreSlim` を使う
- **全て同期メソッド** → `ReaderWriterLockSlim` でも可
- **Reader/Writer 分離 + async が必要** → `AsyncReaderWriterLock`（外部ライブラリ）

## 補足: lock ステートメントも同様

`lock` ステートメント（`Monitor`）もスレッドアフィニティを持つため、`await` と組み合わせるとコンパイルエラー CS1996 になる。C# コンパイラが `lock` ブロック内の `await` を禁止しているのはこの問題を防ぐため。

```csharp
// コンパイルエラー CS1996
lock (_obj)
{
    await SomeAsync(); // Cannot await in the body of a lock statement
}
```

`ReaderWriterLockSlim` はコンパイラによるチェックがないため、実行時まで問題が発見できない点に注意。

## 参考

- [SemaphoreSlim - Microsoft Docs](https://learn.microsoft.com/dotnet/api/system.threading.semaphoreslim)
- [ReaderWriterLockSlim - Microsoft Docs](https://learn.microsoft.com/dotnet/api/system.threading.readerwriterlockslim)
- 該当コミット: `1e354d8` (Fix dotnet tool installation and async lock issue in SolutionManager)
