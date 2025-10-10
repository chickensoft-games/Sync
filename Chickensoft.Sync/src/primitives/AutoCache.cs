namespace Chickensoft.Sync.Primitives;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Collections;
using Sync;

internal static class Cache<T> where T : struct {
  public static T Value { get; set; }
}

/// <summary>
/// A cache that broadcasts the last value pushed to it to all subscribers.
/// </summary>
public interface IAutoCache : IAutoObject<AutoCache.Binding> {
  /// <summary>
  /// Attempts to get the last value which was pushed to the cache of a specific value type.
  /// </summary>
  /// <param name="value"></param>
  /// <typeparam name="T"></typeparam>
  /// <returns></returns>
  bool TryGetValue<T>(out T? value) where T : struct;

  /// <summary>
  /// Attempts to get the last value which was pushed to the cache of a specific reference type.
  /// </summary>
  /// <param name="value"></param>
  /// <typeparam name="T"></typeparam>
  /// <returns></returns>
  bool TryGetValue<T>(out T value) where T : class;
}

/// <summary>
/// A cache that broadcasts the last value pushed to it to all subscribers.
/// </summary>
public sealed class AutoCache : IAutoCache, IPerform<AutoCache.PopOp> {
  // Atomic operations
  private readonly record struct PopOp;

  // Broadcasts
  private readonly record struct RefValue(object Value);

  /// <summary>
  /// A binding to an <see cref="AutoCache"/>.
  /// </summary>
  public class Binding : SyncBinding {
    internal Binding(ISyncSubject subject) : base(subject) { }

    /// <summary>
    /// Registers a callback that is invoked whenever a value is pushed of a specific value type
    /// </summary>
    /// <param name="callback">Callback to invoke.</param>
    /// <param name="condition">Optional condition that must be true for the
    /// callback to be invoked.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnValue<T>(
      Callback<T> callback, Func<T, bool>? condition = null) where T : struct
    {
      bool predicate(T value) => condition?.Invoke(value) ?? true;

      AddCallback(
        (in T broadcast) => callback(broadcast),
        (in T broadcast) => predicate(broadcast)
      );

      return this;
    }

    /// <summary>
    /// Registers a callback that is invoked whenever a value is pushed of a specific reference type
    /// </summary>
    /// <param name="callback">Callback to invoke.</param>
    /// <param name="condition">Optional condition that must be true for the
    /// callback to be invoked.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnValue<T>(
      Action<T> callback, Func<T, bool>? condition = null) where T : class
    {
      bool predicate(T value) => condition?.Invoke(value) ?? true;

      AddCallback(
        (in RefValue value) => {
          if (value.Value is T tValue) {
            callback(tValue);
          }
        },
        (in RefValue value) => value.Value is T tValue && predicate(tValue)
      );

      return this;
    }
  }

  private readonly SyncSubject _subject;
  private readonly Passthrough _passthrough;
  private readonly BoxlessQueue _boxlessQueue;
  private readonly HashSet<Type> _valueSet;
  private readonly Dictionary<Type, object> _refDict;

  /// <summary>
  /// <para>
  /// Creates a new auto cache.
  /// </para>
  /// <para>
  /// A cache that broadcasts the last value pushed to it to all subscribers.
  /// </para>
  /// </summary>
  public AutoCache() {
    _subject = new SyncSubject(this);
    _passthrough = new Passthrough(this);
    _boxlessQueue = new BoxlessQueue();
    _valueSet = [];
    _refDict = [];
  }

  /// <inheritdoc />
  public Binding Bind() => new Binding(_subject);

  /// <inheritdoc />
  public void ClearBindings() => _subject.ClearBindings();

  /// <inheritdoc />
  public void Dispose() => _subject.Dispose();

  /// <inheritdoc />
  public bool TryGetValue<T>(out T? value) where T : struct {
    value = default;
    if (_valueSet.Contains(typeof(T))) {
      value = Cache<T>.Value;
      return true;
    }
    return false;
  }

  /// <inheritdoc />
  public bool TryGetValue<T>([MaybeNullWhen(false)] out T value) where T : class {
    value = null;
    if (_refDict.TryGetValue(typeof(T), out var refVal) &&
        refVal is T derivedRef) {
      value = derivedRef;
      return true;
    }
    return false;
  }

  /// <summary>
  /// Pushes a new value type onto the cache and broadcasts it to all subscribers.
  /// </summary>
  /// <param name="value"></param>
  /// <typeparam name="T"></typeparam>
  public void Push<T>(in T value) where T : struct {
    _valueSet.Add(typeof(T));
    Cache<T>.Value = value;
    _boxlessQueue.Enqueue(value);
    _subject.Perform(new PopOp());
  }

  /// <summary>
  /// Pushes a new reference type onto the cache and broadcasts it to all subscribers.
  /// </summary>
  /// <param name="value"></param>
  /// <typeparam name="T"></typeparam>
  public void Push<T>(T value) where T : class {
    _refDict[typeof(T)] = value;
    _boxlessQueue.Enqueue(new RefValue(value));
    _subject.Perform(new PopOp());
  }

  private void Handle<T>(in T value) where T : struct {
    _subject.Broadcast(value); // invoke callbacks registered for this value
  }

  void IPerform<PopOp>.Perform(in PopOp op) {
    _boxlessQueue.Dequeue(_passthrough);
  }


  /// <summary>
  /// The combined count of all reference types and value types stored in the cache.
  /// </summary>
  public int Count => _refDict.Count + _valueSet.Count;

  /// <summary>
  /// Clears the cache of any stored values.
  /// </summary>
  public void Clear() {
    _refDict.Clear();
    _valueSet.Clear();
  }

  private readonly struct Passthrough : IBoxlessValueHandler {
    public readonly AutoCache Cache { get; }

    public Passthrough(AutoCache cache) {
      Cache = cache;
    }

    public void HandleValue<TValue>(in TValue value) where TValue : struct =>
      Cache.Handle(value);
  }
}
