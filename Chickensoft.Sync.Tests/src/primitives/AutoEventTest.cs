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
    public event Action<TestEventArgs>? TestEvent;
    public void Fire(string message) => TestEvent?.Invoke(new TestEventArgs(message));
  }

  private static AutoEvent<TestEventArgs> Wrap(EventSource source) =>
    new(h => source.TestEvent += h, h => source.TestEvent -= h);

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
  public void ForwardsEventToBindings()
  {
    var source = new EventSource();
    var autoEvent = Wrap(source);
    string? received = null;

    autoEvent.Bind().On(args => received = args.Message);

    source.Fire("hello");

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
      if (args.Message == "first") { source.Fire("second"); }
      inCallback = false;
    });

    source.Fire("first");

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

    source.Fire("hello");
    source.Fire("world");
    source.Fire("hello");

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

    source.Fire("ping");

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

    source.Fire("first");
    messages.ShouldBe(["first"]);

    binding.Dispose();

    source.Fire("second");
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

    source.Fire("a");
    source.Fire("b");
    messages.ShouldBe(["a", "b"]);
    messages.Clear();

    autoEvent.ClearBindings();
    source.Fire("c");
    messages.ShouldBeEmpty();
  }

  [Fact]
  public void DisposeUnsubscribesFromSource()
  {
    var source = new EventSource();
    var autoEvent = Wrap(source);
    var messages = new List<string>();

    autoEvent.Bind().On(args => messages.Add(args.Message));

    source.Fire("before");
    messages.ShouldBe(["before"]);

    autoEvent.Dispose();

    source.Fire("after");
    messages.ShouldBe(["before"]);
  }
}
