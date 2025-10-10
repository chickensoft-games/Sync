namespace Chickensoft.Sync.Primitives;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Collections;
using Sync;

internal abstract class CachedValue {
  public abstract void Clear();
}

internal class CachedValue<T> : CachedValue {
  private T? _value;

  public T? Value {
    get => _value;
    set {
      _value = value;
      HasValue = true;
    }
  }

  public bool HasValue { get; private set; }

  public override void Clear() {
    Value = default;
    HasValue = false;
  }
}

/// <summary>
/// A cache that broadcasts the last value pushed to it to all subscribers.
/// </summary>
public interface IAutoCache : IAutoObject<AutoCache.Binding> {
  /// <summary>
  /// Attempts to get the last value which was pushed to the cache of a specific reference or value type.
  /// </summary>
  /// <param name="value"></param>
  /// <typeparam name="T"></typeparam>
  /// <returns></returns>
  bool TryGetValue<T>(out T value);
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
  private readonly Dictionary<Type, CachedValue> _valueDict;
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
    _valueDict = [];
    _refDict = [];
  }

  /// <inheritdoc />
  public Binding Bind() => new Binding(_subject);

  /// <inheritdoc />
  public void ClearBindings() => _subject.ClearBindings();

  /// <inheritdoc />
  public void Dispose() {
    _subject.Dispose();
    _refDict.Clear();
    _valueDict.Clear();
  }

  /// <inheritdoc />
  public bool TryGetValue<T>([MaybeNullWhen(false)] out T value) {
    value = default;
    if (_valueDict.TryGetValue(typeof(T), out var val) &&
        val is CachedValue<T> { HasValue: true } derivedValue) {
      value = derivedValue.Value!;
	    return true;
    }
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
  public void Update<T>(in T value) where T : struct {
    if (_valueDict.TryGetValue(typeof(T), out var cachedValue)) {
      var cachedValueCast = (CachedValue<T>)cachedValue;
      cachedValueCast.Value = value;
    }
    else {
      var newCachedValue = new CachedValue<T>();
      _valueDict[typeof(T)] = newCachedValue;
      newCachedValue.Value = value;
    }
    _boxlessQueue.Enqueue(value);
    _subject.Perform(new PopOp());
  }

  /// <summary>
  /// Pushes a new reference type onto the cache and broadcasts it to all subscribers.
  /// </summary>
  /// <param name="value"></param>
  /// <typeparam name="T"></typeparam>
  public void Update<T>(T value) where T : class {
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
  public int Count => _refDict.Count + _valueDict.Count;

  /// <summary>
  /// Clears the cache of any stored values.
  /// </summary>
  public void Clear() {
    _refDict.Clear();
    foreach (var (_, value) in _valueDict) {
      value.Clear();
    }
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
