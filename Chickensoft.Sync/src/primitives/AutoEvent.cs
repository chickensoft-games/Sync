namespace Chickensoft.Sync.Primitives;

using System;
using System.Diagnostics.CodeAnalysis;
using Sync;

/// <summary>
/// <para>
/// An observable adapter for a C# event that forwards event firings to
/// subscribers through the sync pipeline.
/// </para>
/// </summary>
/// <typeparam name="TArgs">Type of the event arguments.</typeparam>
public interface IAutoEvent<TArgs> : IAutoObject<AutoEvent<TArgs>.Binding>;

/// <inheritdoc cref="IAutoEvent{TArgs}"/>
public sealed class AutoEvent<TArgs> : IAutoEvent<TArgs>,
  IPerform<AutoEvent<TArgs>.RaiseOp>
{
  // Atomic operations
  private readonly record struct RaiseOp(TArgs Args);

  // Broadcasts
  private readonly record struct RaiseBroadcast(TArgs Args);

  /// <summary>
  /// A binding to an <see cref="AutoEvent{TArgs}"/>.
  /// </summary>
  public class Binding : SyncBinding
  {
    internal Binding(ISyncSubject subject) : base(subject) { }

    /// <summary>
    /// Registers a callback that is invoked whenever the event is raised.
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
    public Binding On(
      Action<TArgs> callback, Func<TArgs, bool>? condition = null
    )
    {
      bool predicate(TArgs args) => condition?.Invoke(args) ?? true;

      AddCallback(
        (in RaiseBroadcast b) => callback(b.Args),
        (in RaiseBroadcast b) => predicate(b.Args)
      );

      return this;
    }
  }

  private readonly SyncSubject _subject;
  private readonly Action<Action<TArgs>> _unsubscribe;

  /// <summary>
  /// <para>
  /// Creates a new auto event that subscribes to a C# event and forwards
  /// firings to its bindings through the sync pipeline.
  /// </para>
  /// </summary>
  /// <param name="subscribe">Action that subscribes the internal handler to
  /// the source event (e.g., <c>h => myObj.MyEvent += h</c>).</param>
  /// <param name="unsubscribe">Action that unsubscribes the internal handler
  /// from the source event (e.g., <c>h => myObj.MyEvent -= h</c>).</param>
  public AutoEvent(
    Action<Action<TArgs>> subscribe,
    Action<Action<TArgs>> unsubscribe
  )
  {
    _subject = new SyncSubject(this);
    _unsubscribe = unsubscribe;
    subscribe(OnEventRaised);
  }

  private void OnEventRaised(TArgs args) =>
    _subject.Perform(new RaiseOp(args));

  /// <inheritdoc />
  public Binding Bind() => new(_subject);

  /// <inheritdoc />
  public void ClearBindings() => _subject.ClearBindings();

  /// <inheritdoc />
  public void Dispose()
  {
    _unsubscribe(OnEventRaised);
    _subject.Dispose();
  }

  void IPerform<RaiseOp>.Perform(in RaiseOp op) =>
    _subject.Broadcast(new RaiseBroadcast(op.Args));
}
