namespace Chickensoft.Sync.Primitives;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

/// <summary>A readonly reference to an observable dictionary (map).</summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public interface IAutoMap<TKey, TValue> :
    IAutoObject<AutoMap<TKey, TValue>.Binding>,
    IReadOnlyDictionary<TKey, TValue> where TKey : notnull {
  /// <summary>
  /// Equality comparer used to determine key equality.
  /// </summary>
  IEqualityComparer<TKey> Comparer { get; }
}

/// <summary>
/// <para>
/// An observable dictionary (map). Adding, removing, and checking membership of
/// items is O(1). Uses a standard .NET <see cref="Dictionary{TKey, TValue}" />
/// as the backing store.
/// </para>
/// <para>
/// This is a single-threaded, synchronous implementation which uses a
/// performant reactive subject to broadcast serialized, re-entrant safe change
/// events.
/// </para>
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed class AutoMap<TKey, TValue> :
    IAutoMap<TKey, TValue>,
    ICollection<KeyValuePair<TKey, TValue>>,
    IDictionary<TKey, TValue>,
    IPerform<AutoMap<TKey, TValue>.AddOp>,
    IPerform<AutoMap<TKey, TValue>.UpdateOp>,
    IPerform<AutoMap<TKey, TValue>.RemoveOp>,
    IPerform<AutoMap<TKey, TValue>.RemoveMatchingOp>,
    IPerform<AutoMap<TKey, TValue>.ClearOp>
    where TKey : notnull {
  // Atomic operations
  private readonly record struct AddOp(TKey Key, TValue Value);
  private readonly record struct UpdateOp(TKey Key, TValue Value);
  private readonly record struct RemoveOp(TKey Key);
  private readonly record struct RemoveMatchingOp(TKey Key, TValue Value);
  private readonly record struct ClearOp();

  // Binding broadcasts
  private readonly record struct AddBroadcast(TKey Key, TValue Value);
  private readonly record struct UpdateBroadcast(
    TKey Key, TValue Previous, TValue Value
  );
  private readonly record struct RemoveBroadcast(TKey Key, TValue Value);
  private readonly record struct ClearBroadcast();

  /// <summary>
  /// A binding to an <see cref="AutoMap{TKey, TValue}" />.
  /// </summary>
  public sealed class Binding : SyncBinding {
    internal Binding(ISyncSubject subject) : base(subject) { }

    /// <summary>
    /// Registers a callback to be invoked when an item is added to the map.
    /// </summary>
    /// <param name="callback">Callback to be invoked.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnAdd(Action<TKey, TValue> callback) {
      AddCallback((in AddBroadcast b) => callback(b.Key, b.Value));

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an item is updated in the map.
    /// </summary>
    /// <param name="callback">Callback to be invoked.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnUpdate(Action<TKey, TValue, TValue> callback) {
      AddCallback(
        (in UpdateBroadcast b) => callback(b.Key, b.Previous, b.Value)
      );

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an item is removed from the map.
    /// </summary>
    /// <param name="callback">Callback to be invoked.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnRemove(Action<TKey, TValue> callback) {
      AddCallback((in RemoveBroadcast b) => callback(b.Key, b.Value));

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an item is removed from the map.
    /// This particular overload only provides the key of the removed item.
    /// </summary>
    /// <param name="callback">Callback to be invoked.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnRemove(Action<TKey> callback) {
      AddCallback((in RemoveBroadcast b) => callback(b.Key));

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when the map is cleared.
    /// </summary>
    /// <param name="callback">Callback to be invoked.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnClear(Action callback) {
      AddCallback((in ClearBroadcast b) => callback());

      return this;
    }
  }

  private readonly Dictionary<TKey, TValue> _map;
  private readonly SyncSubject _subject;

  /// <inheritdoc />
  public IEqualityComparer<TKey> Comparer => _map.Comparer;

  /// <inheritdoc />
  public int Count => _map.Count;

  /// <inheritdoc />
  public bool IsReadOnly => false;

  /// <summary>
  /// Returns a struct enumerator that iterates through the dictionary
  /// efficiently without boxing.
  /// </summary>
  public Dictionary<TKey, TValue>.Enumerator GetEnumerator() =>
    _map.GetEnumerator();

  /// <summary>
  /// Returns a struct enumerator that iterates through the dictionary keys
  /// efficiently without boxing. This can be used with foreach.
  /// </summary>
  public KeyEnumerator Keys => new KeyEnumerator(_map.Keys.GetEnumerator());

  /// <summary>
  /// Returns a struct enumerator that iterates through the dictionary values
  /// efficiently without boxing. This can be used with foreach.
  /// </summary>
  public ValueEnumerator Values =>
    new ValueEnumerator(_map.Values.GetEnumerator());

  IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys =>
    (_map as IReadOnlyDictionary<TKey, TValue>).Keys;

  IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values =>
    (_map as IReadOnlyDictionary<TKey, TValue>).Values;

  ICollection<TKey> IDictionary<TKey, TValue>.Keys =>
    (_map as IDictionary<TKey, TValue>).Keys;

  ICollection<TValue> IDictionary<TKey, TValue>.Values =>
    (_map as IDictionary<TKey, TValue>).Values;

  /// <summary>
  /// Gets or sets the value associated with the specified key. Setting a value
  /// to an existing key will update the value and notify observers. Setting a
  /// value to a new key will add the key-value pair and notify observers.
  /// </summary>
  /// <param name="key">The key of the value to get or set.</param>
  /// <returns>The value associated with the specified key.</returns>
  public TValue this[TKey key] {
    get => _map[key];
    set => _subject.Perform(new UpdateOp(key, value));
  }

  /// <summary>
  /// Create a new observable <see cref="AutoMap{TKey, TValue}" />.
  /// </summary>
  public AutoMap() : this(null, null) { }

  /// <summary>
  /// Create a new observable <see cref="AutoMap{TKey, TValue}" /> containing
  /// the items from the provided enumerable.
  /// </summary>
  /// <param name="items">Items to populate the map with.</param>
  /// <param name="comparer">Key equality comparer.</param>
  public AutoMap(
    IEnumerable<KeyValuePair<TKey, TValue>>? items = null,
    IEqualityComparer<TKey>? comparer = null
  ) {
    _subject = new SyncSubject(this);

    _map = new Dictionary<TKey, TValue>(items ?? [], comparer);
  }

  #region AutoCollection

  /// <inheritdoc />
  public Binding Bind() => new Binding(_subject);

  /// <inheritdoc />
  public void ClearBindings() => _subject.ClearBindings();

  #endregion AutoCollection

  #region IReadOnlyDictionary

  /// <inheritdoc />
  public bool ContainsKey(TKey key) => _map.ContainsKey(key);

  /// <inheritdoc />
#nullable disable warnings // dumb netstandard stuff
  public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) {
    return _map.TryGetValue(key, out value);
  }
#nullable restore warnings // dumb netstandard stuff

  #endregion IReadOnlyDictionary

  #region Enumeration

  IEnumerator IEnumerable.GetEnumerator() => _map.GetEnumerator();
  IEnumerator<KeyValuePair<TKey, TValue>>
    IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() =>
      _map.GetEnumerator();

  #endregion Enumeration

  #region CollectionAndDictionary

  /// <inheritdoc />
  public void Add(TKey key, TValue value) =>
    _subject.Perform(new AddOp(key, value));

  /// <inheritdoc />
  public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

  /// <inheritdoc />
  public void Clear() => _subject.Perform(new ClearOp());

  /// <inheritdoc />
  public bool Contains(KeyValuePair<TKey, TValue> item) => (_map as ICollection<
    KeyValuePair<TKey, TValue>
  >).Contains(item);

  /// <inheritdoc />
  public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) =>
    (_map as ICollection<KeyValuePair<TKey, TValue>>).CopyTo(array, arrayIndex);

  /// <inheritdoc />
  public void Remove(TKey key) => _subject.Perform(new RemoveOp(key));

  bool IDictionary<TKey, TValue>.Remove(TKey key) =>
    throw new NotSupportedException(
      "Cannot support this method since it would require a return value, but " +
      "the removal operation may be deferred if the map is currently busy. " +
      "Please use `void Remove(TKey key)` instead."
    );

  /// <inheritdoc />
  public void Remove(KeyValuePair<TKey, TValue> item) => _subject.Perform(
    new RemoveMatchingOp(item.Key, item.Value)
  );

  bool ICollection<KeyValuePair<TKey, TValue>>.Remove(
    KeyValuePair<TKey, TValue> item
  ) => throw new NotSupportedException(
    "Cannot support this method since it would require a return value, but " +
    "the removal operation may be deferred if the map is currently busy. " +
    "Please use `void Remove(KeyValuePair<TKey, TValue> item)` instead."
  );

  #endregion CollectionAndDictionary

  #region Operations

  void IPerform<AddOp>.Perform(in AddOp op) {
    var key = op.Key;
    var value = op.Value;

    if (_map.TryGetValue(key, out var existing)) {
      _map[key] = value;

      _subject.Broadcast(new UpdateBroadcast(key, existing, value));
      return;
    }

    _map.Add(key, value);

    _subject.Broadcast(new AddBroadcast(key, value));
  }

  void IPerform<UpdateOp>.Perform(in UpdateOp op) {
    var key = op.Key;
    var value = op.Value;

    if (!_map.TryGetValue(key, out var existing)) {
      // perform an add instead
      _map.Add(key, value);
      _subject.Broadcast(new AddBroadcast(key, value));
      return;
    }

    _map[key] = value;

    _subject.Broadcast(new UpdateBroadcast(key, existing, value));
  }

  void IPerform<RemoveOp>.Perform(in RemoveOp op) {
    var key = op.Key;

    if (!_map.Remove(key, out var value)) {
      return;
    }

    _subject.Broadcast(new RemoveBroadcast(key, value));
  }

  void IPerform<RemoveMatchingOp>.Perform(in RemoveMatchingOp op) {
    var key = op.Key;
    var value = op.Value;

    if (
      !(_map as ICollection<KeyValuePair<TKey, TValue>>)
          .Contains(new KeyValuePair<TKey, TValue>(key, value))
    ) {
      return;
    }

    var existing = _map[key];
    _map.Remove(key);

    _subject.Broadcast(new RemoveBroadcast(key, existing));
  }

  void IPerform<ClearOp>.Perform(in ClearOp op) {
    if (_map.Count == 0) { return; }

    _map.Clear();

    _subject.Broadcast(new ClearBroadcast());
  }

  #endregion Operations

  #region Enumerators

#pragma warning disable IDE0251 // these cannot actually be readonly :P

  /// <summary>
  /// A wrapper around <see cref="Dictionary{TKey, TValue}.Enumerator" /> that
  /// avoids boxing when used in a foreach loop.
  /// </summary>
  public struct KeyEnumerator : IEnumerator<TKey> {
    private Dictionary<TKey, TValue>.KeyCollection.Enumerator
      _enumerator;

    /// <summary>
    /// Create a new <see cref="KeyEnumerator" />.
    /// </summary>
    /// <param name="enumerator">The underlying enumerator.</param>
    public KeyEnumerator(
      Dictionary<TKey, TValue>.KeyCollection.Enumerator enumerator
    ) {
      _enumerator = enumerator;
    }

    /// <inheritdoc />
    public readonly TKey Current => _enumerator.Current;

    /// <inheritdoc />
    readonly object IEnumerator.Current => Current;

    /// <inheritdoc />
    public bool MoveNext() => _enumerator.MoveNext();

    /// <inheritdoc />
    public void Dispose() => _enumerator.Dispose();

    /// <inheritdoc />
    public void Reset() => ((IEnumerator)_enumerator).Reset();

    /// <summary>
    /// Returns the underlying enumerator to support efficient iteration with foreach.
    /// </summary>
    public Dictionary<TKey, TValue>.KeyCollection.Enumerator GetEnumerator() =>
      _enumerator;
  }

  /// <summary>
  /// A wrapper around <see cref="Dictionary{TKey, TValue}.Enumerator" /> that
  /// avoids boxing when used in a foreach loop.
  /// </summary>
  public struct ValueEnumerator : IEnumerator<TValue> {
    private Dictionary<TKey, TValue>.ValueCollection.Enumerator
      _enumerator;

    /// <summary>
    /// Create a new <see cref="ValueEnumerator" />.
    /// </summary>
    /// <param name="enumerator">The underlying enumerator.</param>
    public ValueEnumerator(
      Dictionary<TKey, TValue>.ValueCollection.Enumerator enumerator
    ) {
      _enumerator = enumerator;
    }

    /// <inheritdoc />
    public readonly TValue Current => _enumerator.Current;

    /// <inheritdoc />
    readonly object IEnumerator.Current => Current!;

    /// <inheritdoc />
    public bool MoveNext() => _enumerator.MoveNext();

    /// <inheritdoc />
    public void Dispose() => _enumerator.Dispose();

    /// <inheritdoc />
    public void Reset() => ((IEnumerator)_enumerator).Reset();

    /// <summary>
    /// Returns the underlying enumerator to support efficient iteration with foreach.
    /// </summary>
    public Dictionary<TKey, TValue>.ValueCollection.Enumerator
      GetEnumerator() => _enumerator;
  }

#pragma warning restore IDE0251

  #endregion Enumerators

  /// <inheritdoc />
  public void Dispose() => _subject.Dispose();
}
