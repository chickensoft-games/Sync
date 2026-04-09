namespace Chickensoft.Sync.Collections;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public sealed class DisposalBin : ICollection<IDisposable>, ICollection<IAsyncDisposable>, IDisposable, IAsyncDisposable
{
  private readonly List<IDisposable> _disposables = [];
  private readonly List<IAsyncDisposable> _asyncDisposables = [];

  int ICollection<IDisposable>.Count => _disposables.Count;
  bool ICollection<IDisposable>.IsReadOnly => false;
  void ICollection<IDisposable>.Clear() => _disposables.Clear();
  void ICollection<IDisposable>.CopyTo(IDisposable[] array, int arrayIndex) => _disposables.CopyTo(array, arrayIndex);

  int ICollection<IAsyncDisposable>.Count => _asyncDisposables.Count;
  bool ICollection<IAsyncDisposable>.IsReadOnly => false;
  void ICollection<IAsyncDisposable>.Clear() => _asyncDisposables.Clear();
  void ICollection<IAsyncDisposable>.CopyTo(IAsyncDisposable[] array, int arrayIndex) => _asyncDisposables.CopyTo(array, arrayIndex);

  public int Count => _disposables.Count + _asyncDisposables.Count;

  public void Add(IDisposable disposable) => _disposables.Add(disposable);
  public bool Contains(IDisposable disposable) => _disposables.Contains(disposable);
  public bool Remove(IDisposable disposable) => _disposables.Remove(disposable);

  public void Add(IAsyncDisposable asyncDisposable) => _asyncDisposables.Add(asyncDisposable);
  public bool Contains(IAsyncDisposable asyncDisposable) => _asyncDisposables.Contains(asyncDisposable);
  public bool Remove(IAsyncDisposable asyncDisposable) => _asyncDisposables.Remove(asyncDisposable);

  public void Clear()
  {
    _disposables.Clear();
    _asyncDisposables.Clear();
  }

  IEnumerator<IDisposable> IEnumerable<IDisposable>.GetEnumerator() => _disposables.GetEnumerator();
  IEnumerator<IAsyncDisposable> IEnumerable<IAsyncDisposable>.GetEnumerator() => _asyncDisposables.GetEnumerator();

  IEnumerator IEnumerable.GetEnumerator()
  {
    foreach (var disposable in _disposables)
    {
      yield return disposable;
    }

    foreach (var asyncDisposable in _asyncDisposables)
    {
      yield return asyncDisposable;
    }
  }

  public void Dispose()
  {
    if (_asyncDisposables.Count > 0)
    {
      throw new InvalidOperationException($"Cannot dispose the {nameof(DisposalBin)} synchronously because it contains {nameof(IAsyncDisposable)}s: call {nameof(DisposeAsync)} instead.");
    }

    InternalDispose();
  }

  public async ValueTask DisposeAsync()
  {
    while (_asyncDisposables.Count > 0)
    {
      try
      {
        await _asyncDisposables[0].DisposeAsync();
        _asyncDisposables.RemoveAt(0);
      }
      catch (Exception ex)
      {
        throw new DisposeException($"An exception occurred while disposing of {_asyncDisposables[0]} in the {nameof(DisposalBin)}. Remaining disposables may not have been disposed.", ex);
      }
    }

    InternalDispose();
  }

  internal void InternalDispose()
  {
    while (_disposables.Count > 0)
    {
      try
      {
        _disposables[0].Dispose();
        _disposables.RemoveAt(0);
      }
      catch (Exception ex)
      {
        throw new DisposeException($"An exception occurred while disposing of {_disposables[0]} in the {nameof(DisposalBin)}. Remaining disposables may not have been disposed.", ex);
      }
    }
  }
}

public static class DisposalBinExtensions
{
  public static void Collect(this IDisposable disposable, DisposalBin disposalBin) => disposalBin.Add(disposable);
  public static void Collect(this IAsyncDisposable asyncDisposable, DisposalBin disposalBin) => disposalBin.Add(asyncDisposable);
}
