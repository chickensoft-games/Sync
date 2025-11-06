namespace Chickensoft.Sync.Primitives;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Collections;
using Sync;

internal abstract class CachedValue
{
  public abstract void Clear();
}

internal class CachedValue<T> : CachedValue
{
  private T? _value;

  public T? Value
  {
    get => _value;
    set
    {
      _value = value;
      HasValue = true;
    }
  }

  public bool HasValue { get; private set; }

  public override void Clear()
  {
    Value = default;
    HasValue = false;
  }
}

/// <summary>
/// <para>
/// A cache which stores values separated by type.
/// </para>
/// <para>
/// On update, it broadcasts to all bindings and stores the value based on
/// the type given. You can then use the method
/// <see cref="TryGetValue{T}(out T)"/> to get the last value updated of type
/// `T`
/// </para>
/// </summary>
public interface IAutoCache : IAutoObject<AutoCache.Binding>
{
  /// <summary>
  /// Attempts to get the last value which was pushed to the cache of a specific
  /// reference or value type.
  /// </summary>
  /// <param name="value">
  /// When this method returns, contains the value associated with the specified
  /// type, if the type is found; otherwise, the default value for the type of
  /// the value parameter. This parameter is passed uninitialized.
  /// </param>
  /// <typeparam name="T">The type of the value to get</typeparam>
  /// <returns>true if the <see cref="IAutoCache"/> contains an element with the
  /// specified type; otherwise, false.</returns>
  bool TryGetValue<T>([MaybeNullWhen(false)] out T value) where T : notnull;
}

/// <inheritdoc cref="IAutoCache"/>
public sealed class AutoCache : IAutoCache,
  IPerform<AutoCache.PopOp>,
  IPerform<AutoCache.ClearOp>
{
  // Atomic operations
  private readonly record struct PopOp;
  private readonly record struct ClearOp;

  // Broadcasts
  private readonly record struct RefValue(object Value);
  private readonly record struct ClearBroadcast;

  /// <summary>
  /// A binding to an <see cref="AutoCache"/>.
  /// </summary>
  public class Binding : SyncBinding
  {
    internal Binding(ISyncSubject subject) : base(subject) { }

    /// <summary>
    /// Registers a callback that is invoked whenever a value is pushed of a
    /// specific value type
    /// </summary>
    /// <param name="callback">Callback to invoke.</param>
    /// <param name="condition">Optional condition that must be true for the
    /// callback to be invoked.</param>
    /// <typeparam name="T">Value Type to Listen For</typeparam>
    /// <returns>This binding (for chaining).</returns>
    /// <seealso cref="OnUpdate{T}(Action{T}, Func{T, bool}?)"/>
    [
      SuppressMessage(
        "Style",
        "IDE0350",
        Justification = "Implicit lambda with ref type won't compile"
      )
    ]
    public Binding OnUpdate<T>(
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
    /// Registers a callback that is invoked whenever a value is pushed of a
    /// specific reference type
    /// </summary>
    /// <param name="callback">Callback to invoke.</param>
    /// <param name="condition">Optional condition that must be true for the
    /// callback to be invoked.</param>
    /// <typeparam name="T">Ref Type to Listen For</typeparam>
    /// <returns>This binding (for chaining).</returns>
    /// <seealso cref="OnUpdate{T}(Callback{T}, Func{T, bool}?)"/>
    [
      SuppressMessage(
        "Style",
        "IDE0350",
        Justification = "Implicit lambda with ref type won't compile"
      )
    ]
    public Binding OnUpdate<T>(
      Action<T> callback, Func<T, bool>? condition = null) where T : class
    {
      bool predicate(T value) => condition?.Invoke(value) ?? true;

      AddCallback(
        (in RefValue value) => callback(Unsafe.As<T>(value.Value)),
        (in RefValue value) => value.Value is T tValue && predicate(tValue)
      );

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when the cache is cleared.
    /// </summary>
    /// <param name="callback">Callback to be invoked.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnClear(Action callback)
    {
      AddCallback((in ClearBroadcast b) => callback());

      return this;
    }
  }

  private readonly SyncSubject _subject;
  private readonly Passthrough _passthrough;
  private readonly BoxlessQueue _boxlessQueue;
  private readonly Dictionary<Type, CachedValue> _valueDict;
  private readonly Dictionary<Type, object> _refDict;
  private int _cacheCount;

  /// <summary>
  /// <para>
  /// Creates a new auto cache.
  /// </para>
  /// <para>
  /// <inheritdoc cref="AutoCache"/>
  /// </para>
  /// </summary>
  public AutoCache()
  {
    _subject = new SyncSubject(this);
    _passthrough = new Passthrough(this);
    _boxlessQueue = new BoxlessQueue();
    _valueDict = [];
    _refDict = [];
  }

  /// <inheritdoc />
  public Binding Bind() => new(_subject);

  /// <inheritdoc />
  public void ClearBindings() => _subject.ClearBindings();

  /// <inheritdoc />
  public void Dispose()
  {
    _subject.Dispose();
    _refDict.Clear();
    _valueDict.Clear();
  }

  /// <inheritdoc />
  /// <returns>true if the <see cref="AutoCache"/> contains an element with the
  /// specified type; otherwise, false.</returns>
  public bool TryGetValue<T>([MaybeNullWhen(false)] out T value)
    where T : notnull
  {
    value = default;
    if
    (
      _valueDict.TryGetValue(typeof(T), out var val) &&
      val is CachedValue<T> { HasValue: true } derivedValue
    )
    {
      value = derivedValue.Value!;
      return true;
    }
    if
    (
      _refDict.TryGetValue(typeof(T), out var refVal) &&
      refVal is T derivedRef
    )
    {
      value = derivedRef;
      return true;
    }
    return false;
  }
  /// <summary>
  /// <para>
  /// Updates the cache for a given value type and broadcasts it to all
  /// subscribers.
  /// </para>
  /// <para>
  /// To update the cache for a particular base reference type, use the generic
  /// parameter of the type you want to update.
  /// </para>
  /// <para>
  /// I.e., Update&lt;BaseType&gt;(new DerivedType())
  /// </para>
  /// </summary>
  /// <remarks>
  /// Always remember that pushing a struct as an interface or object will box
  /// the value
  /// </remarks>
  /// <seealso cref="Update{T}(T)"/>
  /// <param name="value">Value to update with</param>
  /// <typeparam name="T">Value Type</typeparam>
  public void Update<T>(in T value) where T : struct
  {
    if (_valueDict.TryGetValue(typeof(T), out var cachedValue))
    {
      var cachedValueCast = (CachedValue<T>)cachedValue;
      if (!cachedValueCast.HasValue)
      {
        _cacheCount++;
      }
      cachedValueCast.Value = value;
    }
    else
    {
      var newCachedValue = new CachedValue<T>();
      _valueDict[typeof(T)] = newCachedValue;
      newCachedValue.Value = value;
      _cacheCount++;
    }
    _boxlessQueue.Enqueue(value);
    _subject.Perform(new PopOp());
  }

  /// <summary>
  /// <para>
  /// Updates the cache for a given reference type and broadcasts it to all
  /// subscribers.
  /// </para>
  /// <para>
  /// To update the cache for a particular base reference type, use the generic
  /// parameter of the type you want to update.
  /// </para>
  /// <para>
  /// I.e., Update&lt;BaseType&gt;(new DerivedType())
  /// </para>
  /// <remarks>
  /// Always remember that pushing a struct as an interface or object will box
  /// the value
  /// </remarks>
  /// </summary>
  /// <seealso cref="Update{T}(in T)"/>
  /// <param name="value">Value to update with</param>
  /// <typeparam name="T">Reference Type</typeparam>
  public void Update<T>(T value) where T : class
  {
    _refDict[typeof(T)] = value;
    _boxlessQueue.Enqueue(new RefValue(value));
    _subject.Perform(new PopOp());
  }

  private void Handle<T>(in T value) where T : struct =>
    _subject.Broadcast(value); // invoke callbacks registered for this value

  void IPerform<PopOp>.Perform(in PopOp op) =>
    _boxlessQueue.Dequeue(_passthrough);

  void IPerform<ClearOp>.Perform(in ClearOp op)
  {
    if (Count == 0)
    { return; }

    _refDict.Clear();
    foreach (var (_, value) in _valueDict)
    {
      value.Clear();
    }
    _cacheCount = 0;

    _subject.Broadcast(new ClearBroadcast());
  }


  /// <summary>
  /// The combined count of all reference types and value types stored in the
  /// cache.
  /// </summary>
  public int Count => _refDict.Count + _cacheCount;

  /// <summary>
  /// Clears the cache of any stored values.
  /// </summary>
  public void Clear() => _subject.Perform(new ClearOp());

  private readonly struct Passthrough : IBoxlessValueHandler
  {
    public readonly AutoCache Cache { get; }

    public Passthrough(AutoCache cache)
    {
      Cache = cache;
    }

    public void HandleValue<TValue>(in TValue value) where TValue : struct =>
      Cache.Handle(value);
  }
}
