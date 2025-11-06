namespace Chickensoft.Sync;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

/// <summary>
/// Binding callback that receives an announced broadcast.
/// </summary>
/// <typeparam name="TBroadcast">Broadcast type.</typeparam>
/// <param name="broadcast">Broadcast.</param>
public delegate void Callback<TBroadcast>(in TBroadcast broadcast)
  where TBroadcast : struct;

/// <summary>
/// Predicate that determines whether a callback should be invoked for a given
/// broadcast.
/// </summary>
/// <typeparam name="TBroadcast">Broadcast type.</typeparam>
/// <param name="broadcast">Broadcast.</param>
/// <returns>True if the value passes the predicate, false otherwise.</returns>
public delegate bool Condition<TBroadcast>(in TBroadcast broadcast)
  where TBroadcast : struct;

/// <summary>
/// Base class for creating custom bindings that listen to announcements from
/// a <see cref="SyncSubject" />. A sync binding should be created by a reactive
/// object and only ever assigned as a listener to one sync subject.
/// </summary>
public interface ISyncBinding : IDisposable
{
  /// <summary>
  /// Invokes the callbacks associated with a particular type of broadcast. This
  /// will invoke every callback registered for the specified type of broadcast
  /// which matches their registered predicates (if provided) in the order that
  /// they were added. To protect against re-entry, do not call this method if
  /// you are already processing a broadcast since bindings are expected to
  /// invoke callbacks naively. The base subject implementation,
  /// <see cref="SyncSubject" />, automatically protects against re-entry by
  /// queuing future announcements if one is in progress.
  /// </summary>
  /// <typeparam name="TBroadcast">Type of broadcast whose callbacks should
  /// be invoked.</typeparam>
  /// <param name="broadcast">Broadcast.</param>
  void InvokeCallbacks<TBroadcast>(in TBroadcast broadcast)
    where TBroadcast : struct;
}

/// <summary>
/// Base class for creating custom bindings that listen to announcements from
/// a <see cref="SyncSubject" />. A sync binding should be created by a reactive
/// object and only ever assigned as a listener to one sync subject.
/// </summary>
public abstract class SyncBinding : ISyncBinding
{
  private readonly Dictionary<Type, List<object>>
    _callbacks;

  private static readonly List<object> _emptyCallbacks = [];
  private bool _isDisposed;
  /// <summary>
  /// The subject that this binding is currently bound to, or null if it has been
  /// disposed.
  /// </summary>
  protected internal ISyncSubject? _subject;

  private ObjectDisposedException DisposedException =>
    new("This SyncBinding has been disposed and can no longer be used.");

  /// <summary>
  /// Creates a new <see cref="SyncBinding" />.
  /// </summary>
  protected SyncBinding(ISyncSubject subject)
  {
    _callbacks = [];
    _subject = subject;
    if (subject.IsDisposed)
    {
      throw new ObjectDisposedException(
        nameof(SyncSubject),
        "Cannot create a binding to a disposed subject."
      );
    }
    _subject.AddBinding(this);
  }

  /// <summary>
  /// Registers a callback which will be invoked when a broadcast of the
  /// specified type is announced, optionally checking to see if it passes a
  /// predicate first.
  /// </summary>
  /// <typeparam name="TBroadcast">Broadcast type.</typeparam>
  /// <param name="callback">Callback which receives the broadcast.</param>
  /// <param name="condition">Optional predicate that checks if the
  /// callback should be invoked for a given broadcast. If not specified, the
  /// callback will be invoked for any broadcast of the specified type.</param>
  [
    SuppressMessage(
      "Style",
      "IDE0350",
      Justification = "Implicit lambda with ref type won't compile"
    )
  ]
  protected internal void AddCallback<TBroadcast>(
    Callback<TBroadcast> callback,
    Condition<TBroadcast>? condition = null
  ) where TBroadcast : struct
  {
    if (_isDisposed)
    { throw DisposedException; }

    var type = typeof(TBroadcast);

    if (!_callbacks.TryGetValue(type, out var callbacks))
    {
      _callbacks[type] = callbacks = [];
    }

    var wrappedCallback = condition is null
      ? callback
      : (in TBroadcast broadcast) =>
      {
        if (condition(in broadcast))
        {
          callback(in broadcast);
        }
      };

    callbacks.Add(wrappedCallback);
  }

  /// <inheritdoc />
  public void InvokeCallbacks<TBroadcast>(in TBroadcast broadcast)
    where TBroadcast : struct
  {
    if (_isDisposed)
    { throw DisposedException; }

    var callbacks = GetCallbacks<TBroadcast>();

    for (var i = 0; i < callbacks.Count; i++)
    {
      var callback = Unsafe.As<Callback<TBroadcast>>(callbacks[i]);
      callback(in broadcast);
    }
  }

  /// <summary>
  /// Gets all of the callbacks registered for the specified broadcast type.
  /// </summary>
  /// <typeparam name="TBroadcast">Broadcast type.</typeparam>
  private List<object> GetCallbacks<TBroadcast>()
      where TBroadcast : struct
  {
    var type = typeof(TBroadcast);

    if (!_callbacks.TryGetValue(type, out var callbacks))
    {
      return _emptyCallbacks;
    }

    return callbacks;
  }

  /// <summary>
  /// Cleans up references to other managed objects. Override this method in
  /// derived classes to add custom cleanup logic, but be sure to call the base
  /// implementation.
  /// </summary>
  protected virtual void Cleanup()
  {
    _callbacks.Clear();
    _subject!.RemoveBinding(this);
    _subject = null;
  }

  private void Dispose(bool disposing)
  {
    if (_isDisposed)
    { return; }

    if (disposing)
    {
      Cleanup();
      _isDisposed = true;
    }
  }

  /// <inheritdoc />
  public void Dispose() => Dispose(disposing: true);
}
