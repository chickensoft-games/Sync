namespace Chickensoft.Sync.Disposables;

using System;
using System.Collections;
using System.Collections.Generic;
using Chickensoft.Collections;

/// <summary>Represents a set of disposable resources that are disposed together.</summary>
public sealed class CompositeDisposable : ICollection<IDisposable>, IDisposable
{
  private readonly Set<IDisposable> _disposables = [];

  private bool _isDisposed;

  /// <summary>Gets the number of disposables that are contained in a <see cref="CompositeDisposable"/>.</summary>
  /// <returns>The number of disposables that are contained in the <see cref="CompositeDisposable"/>.</returns>
  public int Count => _disposables.Count;

  bool ICollection<IDisposable>.IsReadOnly => false;
  void ICollection<IDisposable>.Add(IDisposable disposable) => Add(disposable);

  /// <summary>Adds a disposable to the <see cref="CompositeDisposable"/>, or disposes the disposable if the <see cref="CompositeDisposable"/> has already been disposed.</summary>
  /// <param name="disposable">The disposable to add.</param>
  /// <returns><see langword="true"/> if the disposable was successfully added; otherwise, <see langword="false"/>.</returns>
  public bool Add(IDisposable disposable)
  {
    if (_isDisposed)
    {
      disposable.Dispose();
      return false;
    }

    return _disposables.Add(disposable);
  }

  /// <summary>Determines whether the <see cref="CompositeDisposable"/> contains a specific disposable.</summary>
  /// <param name="disposable">The disposable to search for.</param>
  /// <returns><see langword="true"/> if the disposable was found; otherwise, <see langword="false"/>.</returns>
  public bool Contains(IDisposable disposable) => _disposables.Contains(disposable);

  /// <summary>Removes and disposes the specified disposable from the <see cref="CompositeDisposable"/>.</summary>
  /// <param name="disposable">The disposable to remove.</param>
  /// <returns><see langword="true"/> if the disposable is successfully found, removed and disposed; otherwise, <see langword="false"/>.</returns>
  public bool Remove(IDisposable disposable)
  {
    if (!_isDisposed && _disposables.Remove(disposable))
    {
      disposable.Dispose();
      return true;
    }

    return false;
  }

  /// <summary>Removes all disposables from a <see cref="CompositeDisposable"/>, but does not dispose the <see cref="CompositeDisposable"/>.</summary>
  public void Clear()
  {
    foreach (var disposable in _disposables)
    {
      disposable.Dispose();
    }
    _disposables.Clear();
  }

  /// <summary>Copies the disposables contained in the <see cref="CompositeDisposable"/> to an array, starting at a particular array index.</summary>
  /// <param name="array">The array to copy the contained disposables to.</param>
  /// <param name="arrayIndex">The zero-based index at which copying begins.</param>
  public void CopyTo(IDisposable[] array, int arrayIndex) => _disposables.CopyTo(array, arrayIndex);

  /// <summary>Returns an enumerator that iterates through the <see cref="CompositeDisposable"/>.</summary>
  /// <returns>An enumerator to iterate over the disposables.</returns>
  public IEnumerator<IDisposable> GetEnumerator() => _disposables.GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  /// <summary>Disposes all disposables in the <see cref="CompositeDisposable"/> and clears the <see cref="CompositeDisposable"/>.</summary>
  public void Dispose()
  {
    if (!_isDisposed)
    {
      _isDisposed = true;
      Clear();
    }
  }
}
