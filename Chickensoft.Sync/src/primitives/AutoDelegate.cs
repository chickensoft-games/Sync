namespace Chickensoft.Sync.Primitives;

using System;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// <para>
/// An observable adapter for a C# event with a custom delegate type that
/// forwards event firings to subscribers through the sync pipeline.
/// </para>
/// </summary>
/// <typeparam name="TDelegate">The delegate type of the event. Can have any
/// parameter signature — the <c>converter</c> constructor parameter bridges
/// its parameters to a single <typeparamref name="TEventArgs"/> value.
/// </typeparam>
/// <typeparam name="TEventArgs">The type of event arguments. Use a
/// <see cref="ValueTuple"/> to represent multiple delegate parameters as a
/// single value (e.g., <c>(string Message, int Count)</c>).</typeparam>
public interface IAutoDelegate<TDelegate, TEventArgs> :
  IAutoObject<AutoDelegate<TDelegate, TEventArgs>.Binding>
  where TDelegate : Delegate;

/// <inheritdoc cref="IAutoDelegate{TDelegate,TEventArgs}"/>
public sealed class AutoDelegate<TDelegate, TEventArgs> :
  IAutoDelegate<TDelegate, TEventArgs>,
  IPerform<AutoDelegate<TDelegate, TEventArgs>.RaiseOp>
  where TDelegate : Delegate
{
  // Atomic operations
  private readonly record struct RaiseOp(TEventArgs Args);

  // Broadcasts
  private readonly record struct RaiseBroadcast(TEventArgs Args);

  /// <summary>
  /// A binding to an <see cref="AutoDelegate{TDelegate,TEventArgs}"/>.
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
      Action<TEventArgs> callback, Func<TEventArgs, bool>? condition = null
    )
    {
      bool predicate(TEventArgs args) => condition?.Invoke(args) ?? true;

      AddCallback(
        (in RaiseBroadcast b) => callback(b.Args),
        (in RaiseBroadcast b) => predicate(b.Args)
      );

      return this;
    }
  }

  private readonly SyncSubject _subject;
  private readonly Action<TDelegate> _unsubscribe;
  private readonly TDelegate _handler;

  /// <summary>
  /// Creates a new <see cref="AutoDelegate{TDelegate,TEventArgs}"/> that
  /// subscribes to a C# event with a custom delegate type and forwards firings
  /// to its bindings through the sync pipeline.
  /// </summary>
  /// <param name="converter">A function that receives the internal
  /// <see cref="Action{TEventArgs}"/> callback and returns a
  /// <typeparamref name="TDelegate"/> that invokes it. This bridges any
  /// delegate signature to the single <typeparamref name="TEventArgs"/>
  /// value, e.g.:
  /// <c>onEvent => new MyHandler((a, b) => onEvent((a, b)))</c>.
  /// </param>
  /// <param name="subscribe">Action that subscribes the internal handler to
  /// the source event (e.g., <c>h => myObj.MyEvent += h</c>).</param>
  /// <param name="unsubscribe">Action that unsubscribes the internal handler
  /// from the source event (e.g., <c>h => myObj.MyEvent -= h</c>).</param>
  public AutoDelegate(
    Func<Action<TEventArgs>, TDelegate> converter,
    Action<TDelegate> subscribe,
    Action<TDelegate> unsubscribe
  )
  {
    _subject = new SyncSubject(this);
    _unsubscribe = unsubscribe;
    _handler = converter(OnEventRaised);
    subscribe(_handler);
  }

  private void OnEventRaised(TEventArgs args) =>
    _subject.Perform(new RaiseOp(args));

  /// <inheritdoc />
  public Binding Bind() => new(_subject);

  /// <inheritdoc />
  public void ClearBindings() => _subject.ClearBindings();

  /// <inheritdoc />
  public void Dispose()
  {
    _unsubscribe(_handler);
    _subject.Dispose();
  }

  void IPerform<RaiseOp>.Perform(in RaiseOp op) =>
    _subject.Broadcast(new RaiseBroadcast(op.Args));
}
