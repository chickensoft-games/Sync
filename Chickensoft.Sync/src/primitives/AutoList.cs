namespace Chickensoft.Sync.Primitives;

using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>A readonly reference to an observable list.</summary>
/// <typeparam name="T">Item type.</typeparam>
public interface IAutoList<T> :
    IAutoObject<AutoList<T>.Binding>, IReadOnlyList<T> {
  /// <summary>
  /// Equality comparer used to determine item equality.
  /// </summary>
  IEqualityComparer<T> Comparer { get; }

  /// <summary>
  /// Copies the elements of the set to an array, starting at a particular
  /// array index.
  /// </summary>
  /// <param name="array">Destination array.</param>
  /// <param name="arrayIndex">Starting index in the destination array.</param>
  void CopyTo(T[] array, int arrayIndex);

  /// <summary>
  /// Determines whether the list contains a specific item via an O(n) search
  /// using the list's equality comparer.
  /// </summary>
  /// <param name="item">The object to locate in the list.</param>
  bool Contains(T item);

  /// <summary>
  /// Determines the index of a specific item in the list via an O(n) search
  /// using the list's equality comparer.
  /// </summary>
  /// <param name="item">The object to locate in the list.</param>
  /// <returns>The index of the item if found, otherwise -1.</returns>
  int IndexOf(T item);
}

/// <summary>
/// <para>
/// An observable list that uses a <see cref="List{T}" /> as the backing store.
/// </para>
/// <para>
/// This is a single-threaded, synchronous implementation which uses a
/// performant reactive subject to broadcast serialized, re-entrant safe change
/// events.
/// </para>
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
public sealed class AutoList<T> : IAutoList<T>, IList<T>,
    IPerform<AutoList<T>.AddOp>,
    IPerform<AutoList<T>.InsertOp>,
    IPerform<AutoList<T>.UpdateOp>,
    IPerform<AutoList<T>.RemoveOp>,
    IPerform<AutoList<T>.RemoveByItem>,
    IPerform<AutoList<T>.ClearOp> {
  // Atomic operations
  private readonly record struct AddOp(T Item);
  private readonly record struct InsertOp(int Index, T Item);
  private readonly record struct UpdateOp(int Index, T Item);
  private readonly record struct RemoveOp(int Index);
  private readonly record struct RemoveByItem(T Item);
  private readonly record struct ClearOp();

  // Binding broadcasts
  private readonly record struct AddBroadcast(T Item, int Index);
  private readonly record struct UpdateBroadcast(
    T Previous, T Item, int Index
  );
  private readonly record struct RemoveBroadcast(T Item, int Index);
  private readonly record struct ClearBroadcast();

  /// <summary>
  /// A binding to an <see cref="AutoList{T}" />.
  /// </summary>
  public sealed class Binding : SyncBinding {
    internal Binding(ISyncSubject subject) : base(subject) { }

    /// <summary>
    /// Registers a callback to be invoked when an item is added to the list.
    /// </summary>
    /// <param name="callback">Callback which receives the item.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnAdd(Action<T> callback) {
      AddCallback((in AddBroadcast broadcast) => callback(broadcast.Item));

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an item of a specific subtype
    /// is added to the list.
    /// </summary>
    /// <param name="callback">Callback which receives the item.</param>
    /// <typeparam name="TDerived">Subtype of item to listen for.</typeparam>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnAdd<TDerived>(Action<TDerived> callback)
        where TDerived : T {
      AddCallback(
        (in AddBroadcast broadcast) => callback((TDerived)broadcast.Item!),
        (in AddBroadcast broadcast) => broadcast.Item is TDerived
      );

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an item is added to the list.
    /// </summary>
    /// <param name="callback">Callback which receives the item and its index.
    /// </param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnAdd(Action<T, int> callback) {
      AddCallback(
        (in AddBroadcast broadcast) => callback(broadcast.Item, broadcast.Index)
      );

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an item of a specific subtype
    /// is added to the list.
    /// </summary>
    /// <param name="callback">Callback which receives the item and its index.
    /// </param>
    /// <typeparam name="TDerived">Subtype of item to listen for.</typeparam>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnAdd<TDerived>(Action<TDerived, int> callback)
        where TDerived : T {
      AddCallback(
        (in AddBroadcast broadcast) => callback(
          (TDerived)broadcast.Item!, broadcast.Index
        ),
        (in AddBroadcast broadcast) => broadcast.Item is TDerived
      );

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an item in the list is updated.
    /// </summary>
    /// <param name="callback">Callback which receives the previous item and
    /// the next item.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnUpdate(Action<T, T> callback) {
      AddCallback(
        (in UpdateBroadcast broadcast) =>
          callback(broadcast.Previous, broadcast.Item)
      );

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an item of a specific subtype
    /// is updated to another specific type.
    /// </summary>
    /// <typeparam name="TPrevious">Previous item type.</typeparam>
    /// <typeparam name="TValue">Next item type.</typeparam>
    /// <param name="callback">Callback which receives the previous item and
    /// the next item.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnUpdate<TPrevious, TValue>(
      Action<TPrevious, TValue> callback
    ) where TPrevious : T where TValue : T {
      AddCallback(
        (in UpdateBroadcast broadcast) =>
          callback((TPrevious)broadcast.Previous!, (TValue)broadcast.Item!),
        (in UpdateBroadcast broadcast) =>
          broadcast.Item is TValue &&
          broadcast.Previous is TPrevious
      );

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an item in the list is updated.
    /// </summary>
    /// <param name="callback">Callback which receives the previous item,
    /// the next item, and the index of the item.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnUpdate(Action<T, T, int> callback) {
      AddCallback(
        (in UpdateBroadcast broadcast) =>
          callback(broadcast.Previous, broadcast.Item, broadcast.Index)
      );

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an item of a specific subtype
    /// is updated to another specific type.
    /// </summary>
    /// <typeparam name="TPrevious">Previous item type.</typeparam>
    /// <typeparam name="TValue">Next item type.</typeparam>
    /// <param name="callback">Callback which receives the previous item,
    /// the next item, and the index of the item.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnUpdate<TPrevious, TValue>(
      Action<TPrevious, TValue, int> callback
    ) where TPrevious : T where TValue : T {
      AddCallback(
        (in UpdateBroadcast broadcast) =>
          callback(
            (TPrevious)broadcast.Previous!,
            (TValue)broadcast.Item!,
            broadcast.Index
          ),
        (in UpdateBroadcast broadcast) =>
          broadcast.Item is TValue &&
          broadcast.Previous is TPrevious
      );

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an item is removed from the
    /// list.
    /// </summary>
    /// <param name="callback">Callback which receives the removed item.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnRemove(Action<T> callback) {
      AddCallback(
        (in RemoveBroadcast broadcast) => callback(broadcast.Item)
      );

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an item of a specific subtype
    /// is removed from the list.
    /// </summary>
    /// <param name="callback">Callback which receives the removed item.</param>
    /// <typeparam name="TDerived">Subtype of item to listen for.</typeparam>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnRemove<TDerived>(Action<TDerived> callback)
        where TDerived : T {
      AddCallback(
        (in RemoveBroadcast broadcast) => callback((TDerived)broadcast.Item!),
        (in RemoveBroadcast broadcast) => broadcast.Item is TDerived
      );

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an item is removed from the
    /// list.
    /// </summary>
    /// <param name="callback">Callback which receives the removed item and
    /// its index.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnRemove(Action<T, int> callback) {
      AddCallback(
        (in RemoveBroadcast broadcast) =>
          callback(broadcast.Item, broadcast.Index)
      );

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an item of a specific subtype
    /// is removed from the list.
    /// </summary>
    /// <param name="callback">Callback which receives the removed item and
    /// its index.</param>
    /// <typeparam name="TDerived">Subtype of item to listen for.</typeparam>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnRemove<TDerived>(Action<TDerived, int> callback)
        where TDerived : T {
      AddCallback(
        (in RemoveBroadcast broadcast) =>
          callback((TDerived)broadcast.Item!, broadcast.Index),
        (in RemoveBroadcast broadcast) => broadcast.Item is TDerived
      );

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when the list is cleared.
    /// </summary>
    /// <param name="callback">Callback.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnClear(Action callback) {
      AddCallback((in ClearBroadcast _) => callback());

      return this;
    }
  }

  private readonly List<T> _list;
  private readonly SyncSubject _subject;

  /// <inheritdoc />
  public IEqualityComparer<T> Comparer { get; }

  /// <summary>
  /// Creates a new observable <see cref="AutoList{T}" />.
  /// </summary>
  public AutoList() : this(null) { }

  /// <summary>
  /// Creates a new observable <see cref="AutoList{T}" /> containing the
  /// items from the provided enumerable.
  /// </summary>
  /// <param name="items">Initial items for the list.</param>
  /// <param name="comparer">
  /// Equality comparer used to determine item equality. If null, the
  /// default equality comparer for <typeparamref name="T" /> is used.
  /// </param>
  public AutoList(
    IEnumerable<T>? items = null,
    IEqualityComparer<T>? comparer = null
  ) {
    _subject = new(this);
    Comparer = comparer ?? EqualityComparer<T>.Default;
    _list = items is not null
      ? [.. items]
      : [];
  }

  #region AutoCollection

  /// <inheritdoc />
  public Binding Bind() => new Binding(_subject);

  /// <inheritdoc />
  public void ClearBindings() => _subject.ClearBindings();

  #endregion AutoCollection

  #region IList<T>

  /// <inheritdoc />
  public T this[int index] {
    get => _list[index];
    set => _subject.Perform(new UpdateOp(index, value));
  }

  /// <inheritdoc />
  public bool IsReadOnly => false;

  /// <inheritdoc />
  public void Add(T item) => _subject.Perform(new AddOp(item));

  /// <inheritdoc />
  public void Clear() => _subject.Perform(new ClearOp());

  /// <inheritdoc />
  public bool Contains(T item) => IndexOfWithComparer(item) >= 0;

  /// <inheritdoc />
  public void CopyTo(T[] array, int arrayIndex) =>
    _list.CopyTo(array, arrayIndex);

  /// <inheritdoc />
  public int IndexOf(T item) => IndexOfWithComparer(item);

  /// <inheritdoc />
  public void Insert(int index, T item) =>
    _subject.Perform(new InsertOp(index, item));

  /// <inheritdoc />

  // can't implement this because we can't return true/false since we don't know
  // if the item will be in the list at the time the remove happens
  bool ICollection<T>.Remove(T item) =>
    throw new NotSupportedException(
      "Cannot support this method since it would require a return value, but " +
      "the removal operation may be deferred if the list is currently busy. " +
      "Please use `IndexOf(item)` and `RemoveAt(index)` instead."
    );

  /// <inheritdoc />
  public void RemoveAt(int index) => _subject.Perform(new RemoveOp(index));

  /// <summary>
  /// Removes the first occurrence of a specific object from the list.
  /// </summary>
  /// <param name="item">The object to remove from the list.</param>
  public void Remove(T item) => _subject.Perform(new RemoveByItem(item));

  #endregion IList<T>

  #region IReadOnlyList<T>

  /// <inheritdoc />
  public int Count => _list.Count;

  #endregion IReadOnlyList<T>

  #region Enumeration

  /// <inheritdoc />
  IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

  /// <inheritdoc />
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  /// <summary>
  /// Gets a struct enumerator for the <see cref="AutoList{T}"/> for efficient
  /// enumeration.
  /// </summary>
  /// <returns>Struct enumerator.</returns>
  public List<T>.Enumerator GetEnumerator() => _list.GetEnumerator();

  #endregion Enumeration

  private int IndexOfWithComparer(T item) {
    for (var i = 0; i < _list.Count; i++) {
      if (Comparer.Equals(_list[i], item)) {
        return i;
      }
    }

    return -1;
  }

  #region Operations

  void IPerform<AddOp>.Perform(in AddOp op) {
    var item = op.Item;
    _list.Add(item);
    _subject.Broadcast(new AddBroadcast(item, _list.Count - 1));
  }

  void IPerform<InsertOp>.Perform(in InsertOp op) {
    var index = op.Index;
    var item = op.Item;

    if (index < 0 || index > _list.Count) {
      throw new ArgumentOutOfRangeException(
        nameof(index),
        index,
        "Cannot insert an item because the index is out of bounds."
      );
    }

    _list.Insert(index, item);
    _subject.Broadcast(new AddBroadcast(item, index));
  }

  void IPerform<UpdateOp>.Perform(in UpdateOp op) {
    var index = op.Index;
    var item = op.Item;

    if (index < 0 || index >= _list.Count) {
      throw new ArgumentOutOfRangeException(
        nameof(index),
        index,
        "Cannot update an item because the index is out of bounds."
      );
    }

    var previous = _list[index];

    if (!Comparer.Equals(previous, item)) {
      _list[index] = item;
      _subject.Broadcast(new UpdateBroadcast(previous, item, index));
    }
  }

  void IPerform<RemoveOp>.Perform(in RemoveOp op) {
    var index = op.Index;

    if (index < 0 || index >= _list.Count) {
      throw new ArgumentOutOfRangeException(
        nameof(index),
        index,
        "Cannot remove an item because the index is out of bounds."
      );
    }

    var item = _list[index];
    _list.RemoveAt(index);

    _subject.Broadcast(new RemoveBroadcast(item, index));
  }

  void IPerform<RemoveByItem>.Perform(in RemoveByItem op) {
    var item = op.Item;
    var index = IndexOfWithComparer(item);

    if (index < 0) { return; }

    _list.RemoveAt(index);
    _subject.Broadcast(new RemoveBroadcast(item, index));
  }

  void IPerform<ClearOp>.Perform(in ClearOp op) {
    if (_list.Count == 0) { return; }

    _list.Clear();
    _subject.Broadcast(new ClearBroadcast());
  }

  #endregion Operations

  /// <inheritdoc />
  public void Dispose() => _subject.Dispose();
}
