namespace Chickensoft.Sync.Disposables;

using System;

/// <summary>Extension methods associated with the <see cref="IDisposable"/> interface.</summary>
public static class DisposableExtensions
{
  /// <summary>Adds the specified disposable to the given <see cref="CompositeDisposable"/> for automatic disposal.</summary>
  /// <typeparam name="TDisposable">The type of the disposable.</typeparam>
  /// <param name="disposable">The disposable to be added to the <see cref="CompositeDisposable"/>.</param>
  /// <param name="composite">The <see cref="CompositeDisposable"/> to which the <paramref name="disposable"/> will be added.</param>
  /// <returns>The disposable (for chaining).</returns>
  public static TDisposable DisposeWith<TDisposable>(this TDisposable disposable, CompositeDisposable composite)
    where TDisposable : IDisposable
  {
    composite.Add(disposable);
    return disposable;
  }
}
