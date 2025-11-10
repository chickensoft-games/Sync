namespace Chickensoft.Sync.Primitives;

using System;
using System.Diagnostics.CodeAnalysis;
using Collections;
using Sync;

/// <summary>
/// <para>
/// A channel which broadcasts value types.
/// </para>
/// </summary>
public interface IAutoChannel : IAutoObject<AutoChannel.Binding>;

/// <inheritdoc cref="IAutoChannel"/>
public sealed class AutoChannel : IAutoChannel, IPerformAnyOperation
{
  // Atomic operations
  private readonly record struct PopOp;

  /// <summary>
  /// A binding to an <see cref="AutoChannel"/>.
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
    [
      SuppressMessage(
        "Style",
        "IDE0350",
        Justification = "Implicit lambda with ref type won't compile"
      )
    ]
    public Binding On<T>(
      Callback<T> callback, Func<T, bool>? condition = null) where T : struct
    {
      bool predicate(T value) => condition?.Invoke(value) ?? true;

      AddCallback(
        (in T broadcast) => callback(broadcast),
        (in T broadcast) => predicate(broadcast)
      );

      return this;
    }
  }

  private readonly SyncSubject _subject;

  /// <summary>
  /// <para>
  /// Creates a new auto channel.
  /// </para>
  /// <para>
  /// <inheritdoc cref="AutoChannel"/>
  /// </para>
  /// </summary>
  public AutoChannel()
  {
    _subject = new SyncSubject(this);
  }

  /// <inheritdoc />
  public Binding Bind() => new(_subject);

  /// <inheritdoc />
  public void ClearBindings() => _subject.ClearBindings();

  /// <inheritdoc />
  public void Dispose() => _subject.Dispose();

  /// <summary>
  /// <para>
  /// Broadcasts the given value type to all subscribers.
  /// </para>
  /// </summary>
  /// <remarks>
  /// Always remember that pushing a struct as an interface or object will box
  /// the value
  /// </remarks>
  /// <param name="value">Value to update with</param>
  /// <typeparam name="T">Value Type</typeparam>
  public void Send<T>(in T value) where T : struct => _subject.Perform(value);

  void IPerformAnyOperation.Perform<TMessage>(in TMessage message) where TMessage : struct =>
    _subject.Broadcast(message);
}
