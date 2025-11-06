namespace Chickensoft.Sync;

using System;
using System.Collections.Generic;
using Chickensoft.Collections;

/// <summary>
/// A synchronous, single-threaded reactive subject.
/// </summary>
public interface ISyncSubject : IDisposable
{
  /// <summary>
  /// True if this subject has been disposed. Once disposed, no further
  /// operations can be performed on it.
  /// </summary>
  bool IsDisposed { get; }

  /// <summary>
  /// Adds a binding to this subject. If the binding is currently processing
  /// other operations, this is deferred until after the current operations
  /// are completed.
  /// </summary>
  /// <param name="binding">The binding to add.</param>
  void AddBinding(ISyncBinding binding);

  /// <summary>
  /// Removes a binding from this subject. If the binding is currently
  /// processing other operations, this is deferred until after the current
  /// operations are completed.
  /// </summary>
  /// <param name="binding">The binding to remove.</param>
  void RemoveBinding(ISyncBinding binding);

  /// <summary>
  /// Removes all bindings from this subject. If any bindings are currently
  /// processing other operations, this is deferred until after the current
  /// operations are completed.
  /// </summary>
  void ClearBindings();

  /// <summary>
  /// <para>
  /// Publishes a broadcast to all current bindings. If already processing,
  /// this is deferred until after the current operations are completed.
  /// </para>
  /// <para>
  /// Broadcasts are simply value types which contain relevant data.
  /// </para>
  /// </summary>
  /// <typeparam name="TBroadcast">The type of broadcast to send.</typeparam>
  /// <param name="broadcast">The broadcast instance to send.</param>
  void Perform<TBroadcast>(in TBroadcast broadcast)
      where TBroadcast : struct;

  /// <summary>
  /// Clears all pending operations immediately. Safe to call any time before
  /// the subject is disposed.
  /// </summary>
  void Clear();
}

/// <summary>
/// <para>
/// Represents a handler for a particular type of atomic operation. Reactive
/// components which own a <see cref="SyncSubject" /> should implement this
/// interface for each type of operation they wish to perform.
/// </para>
/// <para>
/// To protect against re-entrance, Operations cannot always be performed
/// right away. <see cref="SyncSubject" /> handles the operations event
/// loop without boxing operations and defers execution until any pending
/// operations complete when a new one is added.
/// </para>
/// </summary>
public interface IPerform<TOp> where TOp : struct
{
  /// <summary>
  /// <para>
  /// Represents a handler for a particular type of atomic operation. Reactive
  /// components which own a <see cref="SyncSubject" /> can implement this
  /// method to perform a scheduled operation.
  /// </para>
  /// <para>
  /// To protect against re-entrance, Operations cannot always be performed
  /// right away. <see cref="SyncSubject" /> handles the operations event
  /// loop without boxing operations and defers execution until any pending
  /// operations complete when a new one is added.
  /// </para>
  /// </summary>
  void Perform(in TOp op);
}

/// <summary>
/// <para>
/// In ReactiveX (Rx) terminology, this is a "PublishSubject" that is hot
/// (discards values when there are no listeners), serialized (defers
/// mutations and invocations while already processing to protect against
/// reentry), and is fully synchronous (invocations are made on the same call
/// stack). Bindings are invoked in the order that they are added.
/// </para>
/// <para>
/// Because this is fully synchronous and immediate, value types are given
/// to bindings without boxing them, enabling the design of convenient API's
/// which leverage ephemeral structs for the sole purpose of carrying data
/// in a nice, tidy package. This is a generalization of the bindings first
/// seen in Chickensoft.LogicBlocks.
/// </para>
/// <para>
/// This implementation guarantees atomic operations that can invoke multiple
/// types of callbacks on a single binding before doing the same on the next
/// binding, and so on.
/// </para>
/// <para>
/// Errors encountered from executing binding handlers are immediate and halt
/// processing.
/// </para>
/// </summary>
public sealed class SyncSubject : ISyncSubject
{
  internal enum INTERNAL_OP_TYPE
  {
    ADD_BINDING,
    REMOVE_BINDING,
    CLEAR_BINDINGS,
    PERFORM,
    DISPOSE
  }

  internal record struct INTERNAL_OP(
    INTERNAL_OP_TYPE Operation,
    ISyncBinding? Binding = null
  );

  internal bool _isDisposed;
  internal readonly LinkedHashSet<ISyncBinding> _bindings = [];
  private readonly BoxlessQueue _ops = new();
  private readonly Queue<INTERNAL_OP> _internalOps = [];
  private bool _isBusy = false;
  private object? _owner;
  private ObjectDisposedException DisposedException =>
    new(
      nameof(SyncSubject),
      "Cannot perform operation because the object has been disposed."
    );

  /// <inheritdoc />
  public bool IsDisposed => _isDisposed;

  /// <summary>
  /// True if this subject is currently processing operations. When a subject
  /// is busy, any operations added to it are deferred until the current
  /// operations are completed to protect against reentrance.
  /// </summary>
  public bool IsBusy => _isBusy;

  /// <summary>
  /// <para>
  /// Creates a new sync subject.
  /// </para>
  /// <para>
  /// In ReactiveX (Rx) terminology, this is a "PublishSubject" that is hot
  /// (discards values when there are no listeners), serialized (defers
  /// mutations and invocations while already processing to protect against
  /// reentry), and is fully synchronous (invocations are made on the same call
  /// stack). Bindings are invoked in the order that they are added.
  /// </para>
  /// </summary>
  /// <param name="owner">Object which owns this reactive subject. It will
  /// receive relevant operation callbacks before bindings are invoked so that
  /// the owner can update its state and invoke any relevant bindings. The owner
  /// is typically the reactive object which created this subject.</param>
  public SyncSubject(object owner)
  {
    _owner = owner;
  }

  /// <inheritdoc />
  public void AddBinding(ISyncBinding binding)
  {
    if (_isDisposed)
    { throw DisposedException; }

    _internalOps.Enqueue(
      new INTERNAL_OP(INTERNAL_OP_TYPE.ADD_BINDING, Binding: binding)
    );

    Process();
  }

  /// <inheritdoc />
  public void RemoveBinding(ISyncBinding binding)
  {
    if (_isDisposed)
    { throw DisposedException; }

    _internalOps.Enqueue(
      new INTERNAL_OP(INTERNAL_OP_TYPE.REMOVE_BINDING, Binding: binding)
    );

    Process();
  }

  /// <inheritdoc />
  public void ClearBindings()
  {
    if (_isDisposed)
    { throw DisposedException; }

    _internalOps.Enqueue(new INTERNAL_OP(INTERNAL_OP_TYPE.CLEAR_BINDINGS));

    Process();
  }

  /// <inheritdoc />
  public void Perform<TOp>(in TOp op) where TOp : struct
  {
    if (_isDisposed)
    { throw DisposedException; }

    if (_isBusy)
    {
      _ops.Enqueue(in op);
      _internalOps.Enqueue(new INTERNAL_OP(INTERNAL_OP_TYPE.PERFORM));
      Process();
      return;
    }

    // first broadcast optimization: keep it on the stack
    Process(in op);

    return;
  }

  /// <inheritdoc />
  public void Clear()
  {
    if (_isDisposed)
    { throw DisposedException; }

    _ops.Clear();
  }

  private void Process()
  {
    if (_isBusy)
    { return; } // already busy, no re-entry allowed

    _isBusy = true;

    try
    {
      while (_internalOps.Count > 0)
      {
        var op = _internalOps.Dequeue();

        switch (op.Operation)
        {
          case INTERNAL_OP_TYPE.ADD_BINDING:
            _bindings.Add(op.Binding!);
            break;
          case INTERNAL_OP_TYPE.REMOVE_BINDING:
            _bindings.Remove(op.Binding!);
            break;
          case INTERNAL_OP_TYPE.CLEAR_BINDINGS:
            _bindings.Clear();
            break;
          case INTERNAL_OP_TYPE.PERFORM:
            Perform();
            break;
          case INTERNAL_OP_TYPE.DISPOSE:
            // fun trick: trigger finally block on the way out :D
            goto dispose;
        }
      }
    }
    finally
    {
      _isBusy = false;
    }

    return;

    dispose:
    Dispose(disposing: true);
  }

  // overload for first operation optimization to keep it purely on the stack
  // don't call this if _isBusy is true
  private void Process<TOp>(in TOp op)
      where TOp : struct
  {
    _isBusy = true;

    try
    {
      HandleValue(op);
    }
    finally
    {
      _isBusy = false;
    }

    Process();
  }

  private void Perform()
  {
    // dequeue an operation to perform
    var passthrough = new OpPassthrough(this);
    _ops.Dequeue(in passthrough);
  }

  private void HandleValue<TOp>(in TOp op)
      where TOp : struct
  {
    // allow the object which owns us to handle the value and update its state
    // in a single atomic operation
    if (_owner is IPerform<TOp> handler)
    {
      handler.Perform(in op);
    }
  }

  /// <summary>
  /// Immediately invokes all bindings with the given broadcast. Reactive
  /// components which own this subject should only call this method from a
  /// broadcast handler method implemented for
  /// <see cref="IPerform{TBroadcast}.Perform(in TBroadcast)" />.
  /// </summary>
  public void Broadcast<TBroadcast>(in TBroadcast broadcast)
      where TBroadcast : struct
  {
    if (_isDisposed)
    { throw DisposedException; }

    var wasBusy = _isBusy;

    _isBusy = true;

    try
    {
      foreach (var binding in _bindings)
      {
        binding.InvokeCallbacks(in broadcast);
      }
    }
    finally
    {
      _isBusy = wasBusy;
    }

    // drain any ops that were queued up during bindings invocation (if needed)
    Process();
  }

  private void Cleanup()
  {
    // clear references to other managed objects
    _bindings.Clear();
    _ops.Clear();
    _internalOps.Clear();
    _owner = null;
  }

  private void Dispose(bool disposing)
  {
    if (disposing)
    {
      Cleanup();
    }

    _isDisposed = true;
  }

  /// <inheritdoc />
  public void Dispose()
  {
    if (_isDisposed)
    {
      return;
    }

    if (_isBusy)
    {
      // we're busy doing things up the call stack, so just schedule it later
      _internalOps.Enqueue(new INTERNAL_OP(INTERNAL_OP_TYPE.DISPOSE));
      return;
    }

    Dispose(disposing: true);
  }

  private readonly struct OpPassthrough : IBoxlessValueHandler
  {
    public readonly SyncSubject Target { get; }

    public OpPassthrough(SyncSubject target)
    {
      Target = target;
    }
    public readonly void HandleValue<TValue>(in TValue value)
        where TValue : struct => Target.HandleValue(in value);
  }
}
