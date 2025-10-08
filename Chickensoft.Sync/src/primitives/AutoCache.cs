namespace Utopia;

using System;
using System.Collections.Generic;
using Chickensoft.Collections;
using Chickensoft.Sync;
using Chickensoft.Sync.Primitives;

public interface IAutoCache : IAutoObject<AutoCache.Binding> {
  bool TryGetValue<T>(out T value);
}

public sealed class AutoCache : IAutoCache, IPerform<AutoCache.PopOp> {
  // Atomic operations
  private readonly record struct PopOp;

  // Broadcasts
  private readonly record struct RefValue(object Value);

  public class Binding : SyncBinding {
    internal Binding(ISyncSubject subject) : base(subject) { }

    public Binding OnValue<T>(Callback<T> callback) where T : struct {
      AddCallback(callback);
      return this;
    }

    public Binding OnValue<T>(Action<T> action) {
      AddCallback((in RefValue value) => {
        if (value.Value is T tValue) {
          action(tValue);
        }
      });

      return this;
    }
  }

  private readonly SyncSubject _subject;
  private readonly Passthrough _passthrough;
  private readonly BoxlessQueue _boxlessQueue;
  private readonly Dictionary<Type, ValueType> _valueDict;
  private readonly Dictionary<Type, object> _refDict;

  public AutoCache() {
    _subject = new SyncSubject(this);
    _passthrough = new Passthrough(this);
    _boxlessQueue = new BoxlessQueue();
    _valueDict = [];
    _refDict = [];
  }

  /// <inheritdoc />
  public Binding Bind() => new Binding(_subject);

  /// <inheritdoc />
  public void ClearBindings() => _subject.ClearBindings();

  /// <inheritdoc />
  public void Dispose() => _subject.Dispose();

  public bool TryGetValue<T>(out T? value) {
    value = default;
    if (_valueDict.TryGetValue(typeof(T), out var val) &&
        val is T derivedValue) {
      value = derivedValue;
	    return true;
    }
    if (_refDict.TryGetValue(typeof(T), out var refVal) &&
        refVal is T derivedRef) {
      value = derivedRef;
      return true;
    }
    return false;
  }

  public void Push<T>(in T value) where T : struct {
    _valueDict[typeof(T)] = value;
    _boxlessQueue.Enqueue(value);
    _subject.Perform(new PopOp());
  }

  public void Push(object value) {
    _refDict[value.GetType()] = value;
    _boxlessQueue.Enqueue(new RefValue(value));
    _subject.Perform(new PopOp());
  }

  private void Handle<T>(in T value) where T : struct {
    _subject.Broadcast(value); // invoke callbacks registered for this value
  }

  void IPerform<PopOp>.Perform(in PopOp op) {
    _boxlessQueue.Dequeue(_passthrough);
  }

  public int Count => _refDict.Count + _valueDict.Count;

  private readonly struct Passthrough : IBoxlessValueHandler {
    public readonly AutoCache Cache { get; }

    public Passthrough(AutoCache cache) {
      Cache = cache;
    }

    public void HandleValue<TValue>(in TValue value) where TValue : struct =>
      Cache.Handle(value);
  }
}
