namespace Chickensoft.Sync.Tests.Primitives;

using Shouldly;
using Sync.Primitives;

public sealed class AutoChannelTest
{
  private readonly record struct TestValue(int Value);
  private readonly record struct TestMessage(string Text);

  [Fact]
  public void Initializes()
  {
    var channel = new AutoChannel();
    var received = false;

    channel.Bind()
      .On((in int v) => received = true);

    received.ShouldBeFalse();
  }

  [Fact]
  public void NoReentrancy()
  {
    var channel = new AutoChannel();
    var inCallback = false;
    var reentered = false;

    channel.Bind()
      .On((in int v) =>
      {
        if (inCallback)
        {
          reentered = true; // signals immediate (re-entrant) delivery
        }

        inCallback = true;

        if (v == 1)
        {
          channel.Send(2); // attempt re-entrant send
        }

        inCallback = false;
      });

    channel.Send(1);

    reentered.ShouldBe(false);
  }

  [Fact]
  public void BroadcastsAllSentValues()
  {
    var channel = new AutoChannel();
    var values = new List<int>();

    channel.Bind()
      .On((in int v) => values.Add(v));

    channel.Send(5);
    channel.Send(10);
    channel.Send(10);
    channel.Send(15);

    values.ShouldBe([5, 10, 10, 15]);
  }

  [Fact]
  public void BroadcastsMultipleTypes()
  {
    var channel = new AutoChannel();
    var values = new List<object>();

    channel.Bind()
      .On((in int v) => values.Add(v))
      .On((in double v) => values.Add(v))
      .On((in TestValue v) => values.Add(v))
      .On((in TestMessage v) => values.Add(v));

    channel.Send(42);
    channel.Send(3.14);
    channel.Send(new TestValue(100));
    channel.Send(new TestMessage("hello"));

    values.ShouldBe([42, 3.14, new TestValue(100), new TestMessage("hello")]);
  }


  [Fact]
  public void ConditionalCallbacks()
  {
    var channel = new AutoChannel();
    var values = new List<int>();
    var evenValues = new List<int>();

    channel.Bind()
      .On((in int v) => values.Add(v))
      .On(
        (in int v) => evenValues.Add(v),
        condition: v => v % 2 == 0
      );

    channel.Send(1);
    channel.Send(2);
    channel.Send(3);
    channel.Send(4);
    channel.Send(5);

    values.ShouldBe([1, 2, 3, 4, 5]);
    evenValues.ShouldBe([2, 4]);
  }

  [Fact]
  public void MultipleBindings()
  {
    var channel = new AutoChannel();
    var values1 = new List<int>();
    var values2 = new List<int>();
    var values3 = new List<int>();

    channel.Bind()
      .On((in int v) => values1.Add(v));

    channel.Bind()
      .On((in int v) => values2.Add(v));

    channel.Bind()
      .On((in int v) => values3.Add(v));

    channel.Send(7);
    channel.Send(14);

    values1.ShouldBe([7, 14]);
    values2.ShouldBe([7, 14]);
    values3.ShouldBe([7, 14]);
  }

  [Fact]
  public void DisposedBindingStopsReceiving()
  {
    var channel = new AutoChannel();
    var values = new List<int>();

    var binding = channel.Bind();
    binding.On((in int v) => values.Add(v));

    channel.Send(1);
    values.ShouldBe([1]);

    binding.Dispose();

    channel.Send(2);
    values.ShouldBe([1]); // Should not receive the second value
  }

  [Fact]
  public void ClearsBindings()
  {
    var channel = new AutoChannel();
    var values = new List<int>();

    using var binding = channel.Bind();
    binding.On((in int v) => values.Add(v));

    channel.Send(1);
    channel.Send(2);

    values.ShouldBe([1, 2]);
    values.Clear();

    channel.ClearBindings();
    channel.Send(3);
    values.ShouldBeEmpty();
  }

  [Fact]
  public void Disposes()
  {
    var channel = new AutoChannel();

    channel.Dispose();

    Should.Throw<ObjectDisposedException>(() => channel.Send(2));
  }
}
