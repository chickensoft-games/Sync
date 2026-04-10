namespace Chickensoft.Sync.Disposables;

using System;
using System.Threading.Tasks;

public readonly record struct CascadeDisposable : IDisposable, IAsyncDisposable
{
  public readonly IDisposable? Disposable;
  public readonly IAsyncDisposable? AsyncDisposable;

  private CascadeDisposable(IDisposable? disposable, IAsyncDisposable? asyncDisposable)
  {
    Disposable = disposable;
    AsyncDisposable = asyncDisposable;
  }

  public void Dispose() => Disposable?.Dispose();
  public ValueTask DisposeAsync() => AsyncDisposable?.DisposeAsync() ?? default; // = ValueTask.CompletedTask

  public void Deconstruct(out IDisposable? disposable, out IAsyncDisposable? asyncDisposable)
  {
    disposable = Disposable;
    asyncDisposable = AsyncDisposable;
  }

  public static CascadeDisposable Create(IDisposable disposable) => new(disposable, disposable as IAsyncDisposable);
  public static CascadeDisposable Create(IAsyncDisposable disposable) => new(disposable as IDisposable, disposable);
  public static CascadeDisposable Create<T>(T disposable) where T : IDisposable, IAsyncDisposable => new(disposable, disposable);
}
