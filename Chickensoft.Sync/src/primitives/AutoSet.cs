namespace Chickensoft.Sync.Primitives;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

/// <summary>A readonly reference to an observable set.</summary>
/// <typeparam name="T">Item type.</typeparam>
public interface IAutoSet<T> :
  IAutoObject<AutoSet<T>.Binding>, IReadOnlyCollection<T>
{
  /// <summary>
  /// Equality comparer used to determine item equality (and hashing).
  /// </summary>
  IEqualityComparer<T> Comparer { get; }

  /// <summary>
  /// Adds an item to the set. Returns true if the item was actually added,
  /// false if the item was already present.
  /// </summary>
  bool Contains(T item);

  /// <summary>
  /// Copies the elements of the set to an array, starting at a particular
  /// array index.
  /// </summary>
  /// <param name="array">Destination array.</param>
  /// <param name="arrayIndex">Starting index in the destination array.</param>
  void CopyTo(T[] array, int arrayIndex);
}

/// <summary>
/// <para>
/// An observable set. Adding, removing, and checking membership of items is
/// O(1). Uses a standard .NET <see cref="HashSet{T}" /> as the backing store.
/// </para>
/// <para>
/// This is a single-threaded, synchronous implementation which uses a
/// performant reactive subject to broadcast serialized, re-entrant safe change
/// events.
/// </para>
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
public sealed class AutoSet<T> : IAutoSet<T>, ICollection<T>,
    IPerform<AutoSet<T>.AddOp>,
    IPerform<AutoSet<T>.RemoveOp>,
    IPerform<AutoSet<T>.ClearOp>
{
  private readonly record struct AddOp(T Item);
  private readonly record struct RemoveOp(T Item);
  private readonly record struct ClearOp();

  private readonly record struct AddBroadcast(T Item);
  private readonly record struct RemoveBroadcast(T Item);
  private readonly record struct ClearBroadcast();
  private readonly record struct ModifyBroadcast();

  /// <summary>
  /// A binding to an <see cref="AutoSet{T}" />.
  /// </summary>
  public sealed class Binding : SyncBinding
  {
    internal Binding(ISyncSubject subject) : base(subject) { }

    /// <summary>
    /// Registers a callback to be invoked when an item is added to the set.
    /// </summary>
    /// <param name="callback">Callback to be invoked.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnAdd(Action<T> callback)
    {
      AddCallback((in AddBroadcast b) => callback(b.Item));

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an item of a specific subtype
    /// is added to the set.
    /// </summary>
    /// <param name="callback">Callback to be invoked.</param>
    /// <typeparam name="TDerived">Subtype of item to listen for.</typeparam>
    /// <returns>This binding (for chaining).</returns>
    [
      SuppressMessage(
        "Style",
        "IDE0350",
        Justification = "Implicit lambda with ref type won't compile"
      )
    ]
    public Binding OnAdd<TDerived>(Action<TDerived> callback)
      where TDerived : T
    {
      AddCallback(
        (in AddBroadcast b) => callback((TDerived)b.Item!),
        (in AddBroadcast b) => b.Item is TDerived
      );

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an item is removed from the set.
    /// </summary>
    /// <param name="callback">Callback to be invoked.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnRemove(Action<T> callback)
    {
      AddCallback((in RemoveBroadcast b) => callback(b.Item));

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when an item of a specific subtype
    /// is removed from the set.
    /// </summary>
    /// <param name="callback">Callback to be invoked.</param>
    /// <typeparam name="TDerived">Subtype of item to listen for.</typeparam>
    /// <returns>This binding (for chaining).</returns>
    [
      SuppressMessage(
        "Style",
        "IDE0350",
        Justification = "Implicit lambda with ref type won't compile"
      )
    ]
    public Binding OnRemove<TDerived>(Action<TDerived> callback)
      where TDerived : T
    {
      AddCallback(
        (in RemoveBroadcast b) => callback((TDerived)b.Item!),
        (in RemoveBroadcast b) => b.Item is TDerived
      );

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when the set is cleared.
    /// </summary>
    /// <param name="callback">Callback to be invoked.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnClear(Action callback)
    {
      AddCallback((in ClearBroadcast _) => callback());

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked whenever the set is modified.
    /// </summary>
    /// <param name="callback">Callback to be invoked.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnModify(Action callback)
    {
      AddCallback((in ModifyBroadcast _) => callback());
      return this;
    }
  }

  private readonly HashSet<T> _set;
  private readonly SyncSubject _subject;

  /// <inheritdoc />
  public IEqualityComparer<T> Comparer => _set.Comparer;

  /// <summary>
  /// Creates a new observable <see cref="AutoSet{T}" />.
  /// </summary>
  public AutoSet() : this(null) { }

  /// <summary>
  /// Creates a new observable <see cref="AutoSet{T}" /> containing the
  /// items from the provided enumerable.
  /// </summary>
  /// <param name="items">Initial items for the set.</param>
  /// <param name="comparer">
  /// Equality comparer used to determine item equality. If null, the
  /// default equality comparer for <typeparamref name="T" /> is used.
  /// </param>
  public AutoSet(
    IEnumerable<T>? items = null,
    IEqualityComparer<T>? comparer = null
  )
  {
    _subject = new(this);
    _set = items is not null
      ? new HashSet<T>(items, comparer: comparer)
      : new HashSet<T>(comparer: comparer);
  }

  #region AutoCollection

  /// <inheritdoc />
  public Binding Bind() => new(_subject);

  /// <inheritdoc />
  public void ClearBindings() => _subject.ClearBindings();

  #endregion AutoCollection

  #region ICollection<T>

  /// <summary>
  /// Adds an item to the set and broadcasts if the item was actually added.
  /// </summary>
  public void Add(T item) => _subject.Perform(new AddOp(item));

  /// <inheritdoc />
  public int Count => _set.Count;

  /// <inheritdoc />
  public bool IsReadOnly => false;

  /// <inheritdoc />
  public void Clear() => _subject.Perform(new ClearOp());

  /// <inheritdoc />
  public bool Contains(T item) => _set.Contains(item);

  /// <inheritdoc />
  public void CopyTo(T[] array, int arrayIndex) =>
    _set.CopyTo(array, arrayIndex);

  /// <inheritdoc />
  public void Remove(T item) => _subject.Perform(new RemoveOp(item));

  bool ICollection<T>.Remove(T item) => throw new NotSupportedException(
    "Cannot support this method since it would require a return value, but " +
    "the removal operation may be deferred if the set is currently busy. " +
    "Please use `void Remove(T item)` instead."
  );

  #endregion ICollection<T>

  #region Enumeration

  /// <inheritdoc />
  IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

  /// <inheritdoc />
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  /// <summary>
  /// Gets a struct enumerator for the <see cref="AutoSet{T}"/> for efficient
  /// enumeration.
  /// </summary>
  /// <returns>Struct enumerator.</returns>
  public HashSet<T>.Enumerator GetEnumerator() => _set.GetEnumerator();

  #endregion Enumeration

  #region Operations

  void IPerform<AddOp>.Perform(in AddOp operation)
  {
    if (_set.Add(operation.Item))
    {
      _subject.Broadcast(new AddBroadcast(operation.Item));
      _subject.Broadcast(new ModifyBroadcast());
    }
  }

  void IPerform<RemoveOp>.Perform(in RemoveOp operation)
  {
    if (_set.Remove(operation.Item))
    {
      _subject.Broadcast(new RemoveBroadcast(operation.Item));
      _subject.Broadcast(new ModifyBroadcast());
    }
  }

  void IPerform<ClearOp>.Perform(in ClearOp operation)
  {
    if (_set.Count == 0)
    { return; }

    _set.Clear();

    _subject.Broadcast(new ClearBroadcast());
    _subject.Broadcast(new ModifyBroadcast());
  }

  #endregion Operations

  /// <inheritdoc />
  public void Dispose() => _subject.Dispose();
}
