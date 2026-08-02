namespace Chickensoft.Sync.Primitives;

using System;
using System.Diagnostics.CodeAnalysis;
using Chickensoft.Collections;

/// <summary>
/// <para>
/// A channel which queues value types, and broadcasts them on demand, on a
/// first-in-first-out basis
/// </para>
/// </summary>
public interface IAutoQueue : IAutoObject<AutoQueue.Binding>;

/// <inheritdoc cref="IAutoQueue" />
public sealed class AutoQueue
  : IAutoQueue,
    IPerformAnyOperation,
    IPerform<AutoQueue.DequeueOp>,
    IPerform<AutoQueue.DiscardOp>,
    IPerform<AutoQueue.ClearOp>
{
  // Atomic operations
  private readonly record struct DequeueOp;
  private readonly record struct DiscardOp(int Count);
  private readonly record struct ClearOp;

  // Broadcasts
  private readonly record struct EnqueueBroadcast<T>(T Value)
    where T : struct;
  private readonly record struct DequeueBroadcast<T>(T Value)
    where T : struct;
  private readonly record struct DiscardBroadcast(int Count);
  private readonly record struct ClearBroadcast;
  private readonly record struct ModifyBroadcast;

  /// <summary>
  /// A binding to an <see cref="AutoQueue" />.
  /// </summary>
  public sealed class Binding : SyncBinding
  {
    internal Binding(ISyncSubject subject)
      : base(subject) { }

    /// <summary>
    /// Registers a callback that is invoked whenever a value is enqueued of a
    /// specific value type
    /// </summary>
    /// <param name="callback">Callback to invoke.</param>
    /// <param name="condition">Optional condition that must be true for the
    /// callback to be invoked.</param>
    /// <typeparam name="T">Value Type to Listen For</typeparam>
    /// <returns>This binding (for chaining).</returns>
    [SuppressMessage(
      "Style",
      "IDE0350",
      Justification = "Implicit lambda with ref type won't compile"
    )]
    public Binding OnEnqueue<T>(Callback<T> callback, Func<T, bool>? condition = null)
      where T : struct
    {
      bool predicate(T value) => condition?.Invoke(value) ?? true;

      AddCallback(
        (in EnqueueBroadcast<T> broadcast) => callback(broadcast.Value),
        (in EnqueueBroadcast<T> broadcast) => predicate(broadcast.Value)
      );

      return this;
    }

    /// <summary>
    /// Registers a callback that is invoked whenever a value is dequeued of a
    /// specific value type
    /// </summary>
    /// <param name="callback">Callback to invoke.</param>
    /// <param name="condition">Optional condition that must be true for the
    /// callback to be invoked.</param>
    /// <typeparam name="T">Value Type to Listen For</typeparam>
    /// <returns>This binding (for chaining).</returns>
    [SuppressMessage(
      "Style",
      "IDE0350",
      Justification = "Implicit lambda with ref type won't compile"
    )]
    public Binding OnDequeue<T>(Callback<T> callback, Func<T, bool>? condition = null)
      where T : struct
    {
      bool predicate(T value) => condition?.Invoke(value) ?? true;

      AddCallback(
        (in DequeueBroadcast<T> broadcast) => callback(broadcast.Value),
        (in DequeueBroadcast<T> broadcast) => predicate(broadcast.Value)
      );

      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when items are discarded from the queue.
    /// </summary>
    /// <remarks>
    /// The value passed to the callback is the number of items actually discarded,
    /// and not the number requested.
    /// </remarks>
    /// <param name="callback">Callback to be invoked.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnDiscard(Action<int> callback)
    {
      AddCallback((in DiscardBroadcast broadcast) => callback(broadcast.Count));
      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked when the queue is cleared.
    /// </summary>
    /// <param name="callback">Callback to be invoked.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnClear(Action callback)
    {
      AddCallback((in ClearBroadcast _) => callback());
      return this;
    }

    /// <summary>
    /// Registers a callback to be invoked on any modification to the queue.
    /// </summary>
    /// <param name="callback">Callback to be invoked.</param>
    /// <returns>This binding (for chaining).</returns>
    public Binding OnModify(Action callback)
    {
      AddCallback((in ModifyBroadcast _) => callback());
      return this;
    }
  }

  private readonly struct MessageHandler : IBoxlessValueHandler
  {
    private readonly SyncSubject _subject;

    public MessageHandler(SyncSubject subject)
    {
      _subject = subject;
    }

    public void HandleValue<TValue>(in TValue value)
      where TValue : struct => _subject.Broadcast(new DequeueBroadcast<TValue>(value));
  }

  /// <summary>
  /// Total number of values in the queue.
  /// </summary>
  public int Count => _queue.Count;

  /// <summary>
  /// Returns whether the auto queue has any values.
  /// </summary>
  public bool HasValues => _queue.HasValues;

  private readonly SyncSubject _subject;
  private readonly BoxlessQueue _queue;
  private readonly MessageHandler _handler;

  /// <summary>
  /// <para>
  /// Creates a new auto queue.
  /// </para>
  /// <para>
  /// <inheritdoc cref="AutoQueue"/>
  /// </para>
  /// </summary>
  public AutoQueue()
  {
    _subject = new(this);
    _queue = new();
    _handler = new(_subject);
  }

  /// <inheritdoc />
  public Binding Bind() => new(_subject);

  /// <inheritdoc />
  public void ClearBindings() => _subject.ClearBindings();

  /// <inheritdoc />
  public void Dispose() => _subject.Dispose();

  /// <summary>
  /// Add a message to the queue without boxing it.
  /// </summary>
  /// <typeparam name="T">The type of message to enqueue.</typeparam>
  public void Enqueue<T>(in T value)
    where T : struct => _subject.Perform(value);

  /// <summary>
  /// Dequeues and broadcasts the next value in the queue, if any.
  /// </summary>
  public void Dequeue() => _subject.Perform(new DequeueOp());

  /// <summary>
  /// Discard a number of values from the queue without handling them.
  /// </summary>
  /// <param name="count">
  /// The number of values to discard. If this is greater than the number of
  /// values in the queue, all values will be discarded.
  /// </param>
  public void Discard(int count = 1) => _subject.Perform(new DiscardOp(count));

  /// <summary>Clear all values from the queue.</summary>
  public void Clear() => _subject.Perform(new ClearOp());

  void IPerformAnyOperation.Perform<TOp>(in TOp op)
    where TOp : struct
  {
    if (op is not DequeueOp and not DiscardOp and not ClearOp)
    {
      _queue.Enqueue(op);
      _subject.Broadcast(new EnqueueBroadcast<TOp>(op));
      _subject.Broadcast(new ModifyBroadcast());
    }
  }

  void IPerform<DequeueOp>.Perform(in DequeueOp op)
  {
    if (!_queue.HasValues)
    {
      return;
    }

    _queue.Dequeue(_handler);
    _subject.Broadcast(new ModifyBroadcast());
  }

  void IPerform<DiscardOp>.Perform(in DiscardOp op)
  {
    var count = op.Count;
    if (!_queue.HasValues || count <= 0)
    {
      return;
    }

    var discardCount = count > _queue.Count ? _queue.Count : count;

    _queue.Discard(count);
    _subject.Broadcast(new DiscardBroadcast(discardCount));
    _subject.Broadcast(new ModifyBroadcast());
  }

  void IPerform<ClearOp>.Perform(in ClearOp op)
  {
    if (!_queue.HasValues)
    {
      return;
    }

    _queue.Clear();
    _subject.Broadcast(new ClearBroadcast());
    _subject.Broadcast(new ModifyBroadcast());
  }
}
