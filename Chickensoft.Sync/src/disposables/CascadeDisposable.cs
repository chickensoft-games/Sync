namespace Chickensoft.Sync.Disposables;

using System;
using System.Threading.Tasks;

public struct CascadeDisposable : IEquatable<CascadeDisposable>, IDisposable, IAsyncDisposable
{
  private bool _isDisposed;
  private bool _isAsyncDisposed;

  public IDisposable? Disposable { get; }
  public IAsyncDisposable? AsyncDisposable { get; }

  private CascadeDisposable(IDisposable? disposable, IAsyncDisposable? asyncDisposable)
  {
    Disposable = disposable;
    _isDisposed = disposable == null;
    AsyncDisposable = asyncDisposable;
    _isAsyncDisposed = asyncDisposable == null;
  }

  public readonly bool Equals(CascadeDisposable other)
    => Disposable == other.Disposable
    && AsyncDisposable == other.AsyncDisposable;

  public override readonly bool Equals(object obj)
    => obj is CascadeDisposable other
    && Equals(other);

  public override readonly int GetHashCode()
    => HashCode.Combine(Disposable, AsyncDisposable);

  public void Dispose()
  {
    if (!_isDisposed)
    {
      Disposable!.Dispose();
      _isDisposed = true;
    }
  }

  public async ValueTask DisposeAsync()
  {
    if (!_isAsyncDisposed)
    {
      await AsyncDisposable!.DisposeAsync();
      _isAsyncDisposed = true;
    }

    Dispose();
  }

  public static CascadeDisposable Create(IDisposable disposable) => new(disposable, disposable as IAsyncDisposable);
  public static CascadeDisposable Create(IAsyncDisposable disposable) => new(disposable as IDisposable, disposable);
  public static CascadeDisposable Create<TDisposable>(TDisposable disposable)
    where TDisposable : IDisposable, IAsyncDisposable => new(disposable, disposable);
}
