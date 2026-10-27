namespace Chickensoft.Sync.Tests.Primitives;

using System;
using Shouldly;
using Sync.Primitives;

public sealed class AutoDelegateTest
{
  // Single-param named delegate
  private delegate void SingleParamDelegate(string message);

  // Multi-param named delegate using a tuple parameter
  private delegate void TupleParamDelegate(string message, int count);

  private sealed class EventSource
  {
    public event SingleParamDelegate? SingleEvent;
    public event TupleParamDelegate? TupleEvent;
    public void FireSingle(string message) => SingleEvent?.Invoke(message);
    public void FireTuple(string message, int count) => TupleEvent?.Invoke(message, count);
  }

  private static AutoDelegate<SingleParamDelegate, string> WrapSingle(EventSource source) =>
    new(
      onEvent => message => onEvent(message),
      h => source.SingleEvent += h,
      h => source.SingleEvent -= h
    );

  private static AutoDelegate<TupleParamDelegate, (string Message, int Count)> WrapTuple(EventSource source) =>
    new(
      onEvent => (message, count) => onEvent((message, count)),
      h => source.TupleEvent += h,
      h => source.TupleEvent -= h
    );

  [Fact]
  public void Initializes()
  {
    var source = new EventSource();
    var handler = WrapSingle(source);
    var raised = false;

    handler.Bind().On(_ => raised = true);

    raised.ShouldBeFalse();
  }

  [Fact]
  public void ForwardsSingleParamEventToBindings()
  {
    var source = new EventSource();
    var handler = WrapSingle(source);
    string? received = null;

    handler.Bind().On(msg => received = msg);

    source.FireSingle("hello");

    received.ShouldBe("hello");
  }

  [Fact]
  public void ForwardsTupleParamEventToBindings()
  {
    var source = new EventSource();
    var handler = WrapTuple(source);
    string? receivedMsg = null;
    int? receivedCount = null;

    handler.Bind().On(args =>
    {
      receivedMsg = args.Message;
      receivedCount = args.Count;
    });

    source.FireTuple("hello", 42);

    receivedMsg.ShouldBe("hello");
    receivedCount.ShouldBe(42);
  }

  [Fact]
  public void NoReentrancy()
  {
    var source = new EventSource();
    var handler = WrapSingle(source);
    var inCallback = false;
    var reentered = false;

    handler.Bind().On(msg =>
    {
      if (inCallback) { reentered = true; }
      inCallback = true;
      if (msg == "first") { source.FireSingle("second"); }
      inCallback = false;
    });

    source.FireSingle("first");

    reentered.ShouldBeFalse();
  }

  [Fact]
  public void ConditionalCallbacks()
  {
    var source = new EventSource();
    var handler = WrapSingle(source);
    var all = new List<string>();
    var helloOnly = new List<string>();

    handler.Bind()
      .On(msg => all.Add(msg))
      .On(msg => helloOnly.Add(msg), condition: msg => msg == "hello");

    source.FireSingle("hello");
    source.FireSingle("world");
    source.FireSingle("hello");

    all.ShouldBe(["hello", "world", "hello"]);
    helloOnly.ShouldBe(["hello", "hello"]);
  }

  [Fact]
  public void TupleParamConditionalCallbacks()
  {
    var source = new EventSource();
    var handler = WrapTuple(source);
    var all = new List<(string, int)>();
    var highOnly = new List<(string, int)>();

    handler.Bind()
      .On(args => all.Add((args.Message, args.Count)))
      .On(
        args => highOnly.Add((args.Message, args.Count)),
        condition: args => args.Count > 10
      );

    source.FireTuple("low", 5);
    source.FireTuple("high", 42);

    all.ShouldBe([("low", 5), ("high", 42)]);
    highOnly.ShouldBe([("high", 42)]);
  }

  [Fact]
  public void MultipleBindings()
  {
    var source = new EventSource();
    var handler = WrapSingle(source);
    var messages1 = new List<string>();
    var messages2 = new List<string>();

    handler.Bind().On(msg => messages1.Add(msg));
    handler.Bind().On(msg => messages2.Add(msg));

    source.FireSingle("ping");

    messages1.ShouldBe(["ping"]);
    messages2.ShouldBe(["ping"]);
  }

  [Fact]
  public void DisposedBindingStopsReceiving()
  {
    var source = new EventSource();
    var handler = WrapSingle(source);
    var messages = new List<string>();

    var binding = handler.Bind();
    binding.On(msg => messages.Add(msg));

    source.FireSingle("first");
    messages.ShouldBe(["first"]);

    binding.Dispose();

    source.FireSingle("second");
    messages.ShouldBe(["first"]);
  }

  [Fact]
  public void ClearsBindings()
  {
    var source = new EventSource();
    var handler = WrapSingle(source);
    var messages = new List<string>();

    using var binding = handler.Bind();
    binding.On(msg => messages.Add(msg));

    source.FireSingle("a");
    source.FireSingle("b");
    messages.ShouldBe(["a", "b"]);
    messages.Clear();

    handler.ClearBindings();
    source.FireSingle("c");
    messages.ShouldBeEmpty();
  }

  [Fact]
  public void DisposeUnsubscribesFromSource()
  {
    var source = new EventSource();
    var handler = WrapSingle(source);
    var messages = new List<string>();

    handler.Bind().On(msg => messages.Add(msg));

    source.FireSingle("before");
    messages.ShouldBe(["before"]);

    handler.Dispose();

    source.FireSingle("after");
    messages.ShouldBe(["before"]);
  }
}
