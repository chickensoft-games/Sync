namespace Chickensoft.Sync.Primitives;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// <para>
/// An observable value that can change over time.
/// </para>
/// <para>
/// In ReactiveX terminology, this is equivalent to a BehaviorSubject. This
/// particular implementation is "hot" (discards values when there are no
/// listeners), serialized (defers mutations and invocations while already
/// processing to protect against reentry), and is fully synchronous
/// (invocations are made on the same call stack). Bindings are invoked in
/// the order that they are added.
/// </para>
/// </summary>
/// <typeparam name="T">Type of the value.</typeparam>
public interface IAutoValue<T> : IAutoObject<AutoValue<T>.Binding>
{
  /// <summary>Current value.</summary>
  T Value { get; }

  /// <summary>
  /// Equality comparer used to determine value equality.
  /// </summary>
  IEqualityComparer<T> Comparer { get; }
}

/// <summary>
/// <para>
/// An observable value that can change over time.
/// </para>
/// <para>
/// In ReactiveX terminology, this is equivalent to a BehaviorSubject. This
/// particular implementation is "hot" (discards values when there are no
/// listeners), serialized (defers mutations and invocations while already
/// processing to protect against reentry), and is fully synchronous
/// (invocations are made on the same call stack). Bindings are invoked in
/// the order that they are added.
/// </para>
/// </summary>
/// <typeparam name="T">Type of the value.</typeparam>
public sealed class AutoValue<T> : IAutoValue<T>,
    IPerform<AutoValue<T>.UpdateOp>,
    IPerform<AutoValue<T>.SyncOp>,
    IPerform<AutoValue<T>.SyncDerivedOp>
{
  // Atomic operations
  private readonly record struct UpdateOp(T Value);
  //    these 2 sync operations are used to invoke callbacks as soon as possible
  //    after they're added to mimic a BehaviorSubject
  private readonly record struct SyncOp(
    Action<T> Callback, Func<T, bool> Condition
  );
  private readonly record struct SyncDerivedOp(
    Action<T> Callback, Func<T, bool> Condition
  );

  // Broadcasts
  private readonly record struct UpdateBroadcast(T Value);

  /// <summary>
  /// A binding to an <see cref="AutoValue{T}"/>.
  /// </summary>
  public class Binding : SyncBinding
  {
    internal Binding(ISyncSubject subject) : base(subject) { }

    /// <summary>
    /// Registers a callback that is invoked whenever the value changes.
    /// </summary>
    /// <param name="callback">Callback to invoke.</param>
    /// <param name="condition">Optional condition that must be true for the
    /// callback to be invoked.</param>
    /// <returns>This binding (for chaining).</returns>
    [
      SuppressMessage(
        "Style",
        "IDE0350",
        Justification = "Implicit lambda with ref type won't compile"
      )
    ]
    public Binding OnValue(
      Action<T> callback, Func<T, bool>? condition = null
    )
    {
      bool predicate(T value) => condition?.Invoke(value) ?? true;

      AddCallback(
        (in UpdateBroadcast broadcast) => callback(broadcast.Value),
        (in UpdateBroadcast broadcast) => predicate(broadcast.Value)
      );

      // schedule synchronization invocation for this callback with the current value
      _subject!.Perform(new SyncOp(callback, predicate));

      return this;
    }

    /// <summary>
    /// Registers a callback that is invoked whenever the value changes, but
    /// only if the new value is of the specified derived type.
    /// </summary>
    /// <param name="callback">Callback to invoke.</param>
    /// <param name="condition">Optional condition that must be true for the
    /// callback to be invoked.</param>
    /// <typeparam name="TDerived">Subtype of value to listen for.</typeparam>
    /// <returns>This binding (for chaining).</returns>
    [
      SuppressMessage(
        "Style",
        "IDE0350",
        Justification = "Implicit lambda with ref type won't compile"
      )
    ]
    public Binding OnValue<TDerived>(
      Action<TDerived> callback, Func<T, bool>? condition = null
    ) where TDerived : T
    {

      bool predicate(T value) =>
        value is TDerived && (condition?.Invoke(value) ?? true);

      AddCallback(
        (in UpdateBroadcast broadcast) => callback((TDerived)broadcast.Value!),
        (in UpdateBroadcast broadcast) => predicate(broadcast.Value)
      );

      // schedule synchronization invocation for this callback with the current value
      _subject!.Perform(
        new SyncDerivedOp(
          Callback: (T value) => callback((TDerived)value!),
          Condition: predicate
        )
      );

      return this;
    }
  }

  private T _value;
  private readonly SyncSubject _subject;

  /// <inheritdoc />
  public IEqualityComparer<T> Comparer { get; }

  /// <inheritdoc />
  public T Value
  {
    get => _value;
    set => _subject.Perform(new UpdateOp(value));
  }

  /// <summary>
  /// <para>
  /// Creates a new auto value with the given initial value and optional
  /// equality comparer.
  /// </para>
  /// <para>
  /// An AutoValue is an observable value that can change over time.
  /// </para>
  /// <para>
  /// In ReactiveX terminology, this is equivalent to a BehaviorSubject. This
  /// particular implementation is "hot" (discards values when there are no
  /// listeners), serialized (defers mutations and invocations while already
  /// processing to protect against reentry), and is fully synchronous
  /// (invocations are made on the same call stack). Bindings are invoked in
  /// the order that they are added.
  /// </para>
  /// </summary>
  /// <param name="value">Initial value.</param>
  /// <param name="comparer">Equality comparer used to determine value
  /// equality. If null, the default equality comparer for the type is used.
  /// </param>
  public AutoValue(T value, IEqualityComparer<T>? comparer = null)
  {
    _value = value;
    _subject = new(this);
    Comparer = comparer ?? EqualityComparer<T>.Default;
  }

  /// <inheritdoc />
  public Binding Bind() => new(_subject);

  /// <inheritdoc />
  public void ClearBindings() => _subject.ClearBindings();

  /// <inheritdoc />
  public void Dispose() => _subject.Dispose();

  void IPerform<UpdateOp>.Perform(in UpdateOp op)
  {
    if (Comparer.Equals(_value, op.Value))
    {
      return;
    }

    _value = op.Value;

    // announce change to relevant binding callbacks
    _subject.Broadcast(new UpdateBroadcast(op.Value));
  }

  void IPerform<SyncOp>.Perform(in SyncOp op)
  {
    if (op.Condition(_value))
    {
      // synchronize specific callback as soon as possible after its initialization
      op.Callback(_value);
    }
  }

  void IPerform<SyncDerivedOp>.Perform(in SyncDerivedOp op)
  {
    if (op.Condition(_value))
    {
      // synchronize specific callback as soon as possible after its initialization
      op.Callback(_value);
    }
  }
}
