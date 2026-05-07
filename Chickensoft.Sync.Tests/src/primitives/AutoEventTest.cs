namespace Chickensoft.Sync.Tests.Primitives;

using System;
using Shouldly;
using Sync.Primitives;

public sealed class AutoEventTest
{
  private sealed class TestEventArgs(string message)
  {
    public string Message { get; } = message;
  }

  private sealed class EventSource
  {
    public event Action? TestEventZero;
    public event Action<TestEventArgs>? TestEventOne;
    public event Action<object, TestEventArgs>? TestEventTwo;
    public void FireZero() => TestEventZero?.Invoke();
    public void FireOne(string message) => TestEventOne?.Invoke(new TestEventArgs(message));
    public void FireTwo(string message) => TestEventTwo?.Invoke(this, new TestEventArgs(message));
  }

  private static AutoEvent<TestEventArgs> Wrap(EventSource source) =>
    new(h => source.TestEventOne += h, h => source.TestEventOne -= h);

  [Fact]
  public void Initializes()
  {
    var source = new EventSource();
    var autoEvent = Wrap(source);
    var raised = false;

    autoEvent.Bind().On(_ => raised = true);

    raised.ShouldBeFalse();
  }

  [Fact]
  public void ForwardsEventToBindingsWithParamsZero()
  {
    var source = new EventSource();
    var autoEvent = new AutoEvent(
      action => source.TestEventZero += action,
      action => source.TestEventZero -= action
    );
    bool? received = null;

    autoEvent.Bind().On(() => received = true);

    source.FireZero();

    received.ShouldBe(true);
  }

  [Fact]
  public void ForwardsEventToBindingsWithParamsOne()
  {
    var source = new EventSource();
    var autoEvent = Wrap(source);
    string? received = null;

    autoEvent.Bind().On(args => received = args.Message);

    source.FireOne("hello");

    received.ShouldBe("hello");
  }

  [Fact]
  public void ForwardsEventToBindingsWithParamsTwo()
  {
    var source = new EventSource();
    var autoEvent = new AutoEvent<object, TestEventArgs>(
      action => source.TestEventTwo += action,
      action => source.TestEventTwo -= action
    );
    string? received = null;
    object? obj = null;

    autoEvent.Bind().On((sender, args) =>
    {
      received = args.Message;
      obj = sender;
    });

    source.FireTwo("hello");

    obj.ShouldBe(source);
    received.ShouldBe("hello");
  }

  [Fact]
  public void NoReentrancy()
  {
    var source = new EventSource();
    var autoEvent = Wrap(source);
    var inCallback = false;
    var reentered = false;

    autoEvent.Bind().On(args =>
    {
      if (inCallback) { reentered = true; }
      inCallback = true;
      if (args.Message == "first") { source.FireOne("second"); }
      inCallback = false;
    });

    source.FireOne("first");

    reentered.ShouldBeFalse();
  }

  [Fact]
  public void ConditionalCallbacks()
  {
    var source = new EventSource();
    var autoEvent = Wrap(source);
    var all = new List<string>();
    var helloOnly = new List<string>();

    autoEvent.Bind()
      .On(args => all.Add(args.Message))
      .On(args => helloOnly.Add(args.Message), condition: args => args.Message == "hello");

    source.FireOne("hello");
    source.FireOne("world");
    source.FireOne("hello");

    all.ShouldBe(["hello", "world", "hello"]);
    helloOnly.ShouldBe(["hello", "hello"]);
  }

  [Fact]
  public void MultipleBindings()
  {
    var source = new EventSource();
    var autoEvent = Wrap(source);
    var messages1 = new List<string>();
    var messages2 = new List<string>();

    autoEvent.Bind().On(args => messages1.Add(args.Message));
    autoEvent.Bind().On(args => messages2.Add(args.Message));

    source.FireOne("ping");

    messages1.ShouldBe(["ping"]);
    messages2.ShouldBe(["ping"]);
  }

  [Fact]
  public void DisposedBindingStopsReceiving()
  {
    var source = new EventSource();
    var autoEvent = Wrap(source);
    var messages = new List<string>();

    var binding = autoEvent.Bind();
    binding.On(args => messages.Add(args.Message));

    source.FireOne("first");
    messages.ShouldBe(["first"]);

    binding.Dispose();

    source.FireOne("second");
    messages.ShouldBe(["first"]);
  }

  [Fact]
  public void ClearsBindings()
  {
    var source = new EventSource();
    var autoEvent = Wrap(source);
    var messages = new List<string>();

    using var binding = autoEvent.Bind();
    binding.On(args => messages.Add(args.Message));

    source.FireOne("a");
    source.FireOne("b");
    messages.ShouldBe(["a", "b"]);
    messages.Clear();

    autoEvent.ClearBindings();
    source.FireOne("c");
    messages.ShouldBeEmpty();
  }

  [Fact]
  public void DisposeUnsubscribesFromSource()
  {
    var source = new EventSource();
    var autoEvent = Wrap(source);
    var messages = new List<string>();

    autoEvent.Bind().On(args => messages.Add(args.Message));

    source.FireOne("before");
    messages.ShouldBe(["before"]);

    autoEvent.Dispose();

    source.FireOne("after");
    messages.ShouldBe(["before"]);
  }
}
