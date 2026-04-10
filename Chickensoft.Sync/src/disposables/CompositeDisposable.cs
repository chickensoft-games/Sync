namespace Chickensoft.Sync.Disposables;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.Collections;

public sealed class CompositeDisposable : ICollection<CascadeDisposable>, IDisposable, IAsyncDisposable
{
  private readonly Set<CascadeDisposable> _disposables = [];

  public bool IsDisposed { get; private set; }

  public int Count => _disposables.Count;
  public bool IsReadOnly => false;

  void ICollection<CascadeDisposable>.Add(CascadeDisposable disposable) => Add(disposable);

  public bool Add(CascadeDisposable disposable)
  {
    if (IsDisposed)
    {
      disposable.Dispose();
      return false;
    }

    return _disposables.Add(disposable);
  }

  public async ValueTask<bool> AddAsync(CascadeDisposable disposable)
  {
    if (IsDisposed)
    {
      await disposable.DisposeAsync();
      return false;
    }

    return _disposables.Add(disposable);
  }

  public bool Contains(CascadeDisposable disposable) => _disposables.Contains(disposable);

  public bool Remove(CascadeDisposable disposable)
  {
    if (!IsDisposed && _disposables.Remove(disposable))
    {
      disposable.Dispose();
      return true;
    }

    return false;
  }

  public async ValueTask<bool> RemoveAsync(CascadeDisposable disposable)
  {
    if (!IsDisposed && _disposables.Remove(disposable))
    {
      await disposable.DisposeAsync();
      return true;
    }

    return false;
  }

  public void Clear()
  {
    foreach (var disposable in _disposables)
    {
      disposable.Dispose();
    }
    _disposables.Clear();
  }

  public ValueTask ClearAsync()
  {
    var disposeTasks = _disposables.Select(disposable => disposable.DisposeAsync().AsTask()).ToArray();
    _disposables.Clear();
    return new ValueTask(Task.WhenAll(disposeTasks));
  }

  public void CopyTo(CascadeDisposable[] array, int arrayIndex) => _disposables.CopyTo(array, arrayIndex);

  public IEnumerator<CascadeDisposable> GetEnumerator() => _disposables.GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public void Dispose()
  {
    if (!IsDisposed)
    {
      IsDisposed = true;
      Clear();
    }
  }

  public ValueTask DisposeAsync()
  {
    if (!IsDisposed)
    {
      IsDisposed = true;
      return ClearAsync();
    }
    return default; // = ValueTask.CompletedTask
  }
}

public static class DisposableExtensions
{
  public static void DisposeWith(this IDisposable disposable, CompositeDisposable composite) => composite.Add(CascadeDisposable.Create(disposable));
  public static void DisposeWith(this IAsyncDisposable disposable, CompositeDisposable composite) => composite.Add(CascadeDisposable.Create(disposable));
  public static void DisposeWith<TDisposable>(this TDisposable disposable, CompositeDisposable composite)
    where TDisposable : IDisposable, IAsyncDisposable => composite.Add(CascadeDisposable.Create(disposable));
}
