namespace Chickensoft.Sync.Tests.Primitives;

using Chickensoft.Sync.Primitives;
using Shouldly;

public sealed class AutoQueueTest
{
  private readonly record struct TestValue(int Value);
  private readonly record struct TestMessage(string Text);

  [Fact]
  public void Initializes()
  {
    var queue = new AutoQueue();
    var enqueueReceived = false;
    var dequeueReceived = false;
    var discardReceived = false;
    var clearReceived = false;
    var modifyReceived = false;

    queue.Bind()
      .OnEnqueue((in int v) => enqueueReceived = true)
      .OnDequeue((in int v) => dequeueReceived = true)
      .OnDiscard(count => discardReceived = true)
      .OnClear(() => clearReceived = true)
      .OnModify(() => modifyReceived = true);

    queue.Count.ShouldBe(0);
    queue.HasValues.ShouldBeFalse();
    enqueueReceived.ShouldBeFalse();
    dequeueReceived.ShouldBeFalse();
    discardReceived.ShouldBeFalse();
    clearReceived.ShouldBeFalse();
    modifyReceived.ShouldBeFalse();
  }

  [Fact]
  public void NoReentrancy()
  {
    var queue = new AutoQueue();
    var inEnqueueCallback = false;
    var inDequeueCallback = false;
    var enqueueReentered = false;
    var dequeueReentered = false;

    queue.Bind()
      .OnEnqueue((in int v) =>
      {
        if (inEnqueueCallback)
        {
          enqueueReentered = true; // signals immediate (re-entrant) delivery
        }

        inEnqueueCallback = true;

        if (v == 1)
        {
          queue.Enqueue(2); // attempt re-entrant enqueue
        }

        inEnqueueCallback = false;
      })
      .OnDequeue((in int v) =>
      {
        if (inDequeueCallback)
        {
          dequeueReentered = true; // signals immediate (re-entrant) delivery
        }

        inDequeueCallback = true;

        if (v == 1)
        {
          queue.Dequeue(); // attempt re-entrant dequeue
        }

        inDequeueCallback = false;
      });

    queue.Enqueue(1);
    queue.Dequeue();

    enqueueReentered.ShouldBeFalse();
    dequeueReentered.ShouldBeFalse();
  }

  [Fact]
  public void BroadcastsAllEnqueuedDequeuedValues()
  {
    var queue = new AutoQueue();
    var enqueueValues = new List<int>();
    var dequeueValues = new List<int>();
    var modifyCount = 0;

    queue.Bind()
      .OnEnqueue((in int v) => enqueueValues.Add(v))
      .OnDequeue((in int v) => dequeueValues.Add(v))
      .OnModify(() => modifyCount++);

    queue.Enqueue(5);
    queue.Enqueue(10);
    queue.Enqueue(10);
    queue.Enqueue(15);

    queue.Count.ShouldBe(4);
    enqueueValues.ShouldBe([5, 10, 10, 15]);
    dequeueValues.ShouldBe([]);
    modifyCount.ShouldBe(4);

    while (queue.HasValues)
    {
      queue.Dequeue();
    }

    queue.Count.ShouldBe(0);
    dequeueValues.ShouldBe([5, 10, 10, 15]);
    modifyCount.ShouldBe(8);
  }

  [Fact]
  public void BroadcastsMultipleTypes()
  {
    var queue = new AutoQueue();
    var enqueueValues = new List<object>();
    var dequeueValues = new List<object>();
    var modifyCount = 0;

    queue.Bind()
      .OnEnqueue((in int v) => enqueueValues.Add(v))
      .OnEnqueue((in double v) => enqueueValues.Add(v))
      .OnEnqueue((in TestValue v) => enqueueValues.Add(v))
      .OnEnqueue((in TestMessage v) => enqueueValues.Add(v))
      .OnDequeue((in int v) => dequeueValues.Add(v))
      .OnDequeue((in double v) => dequeueValues.Add(v))
      .OnDequeue((in TestValue v) => dequeueValues.Add(v))
      .OnDequeue((in TestMessage v) => dequeueValues.Add(v))
      .OnModify(() => modifyCount++);

    queue.Enqueue(42);
    queue.Enqueue(3.14);
    queue.Enqueue(new TestValue(100));
    queue.Enqueue(new TestMessage("hello"));

    queue.Count.ShouldBe(4);
    enqueueValues
      .ShouldBe([42, 3.14, new TestValue(100), new TestMessage("hello")]);
    dequeueValues.ShouldBe([]);
    modifyCount.ShouldBe(4);

    while (queue.HasValues)
    {
      queue.Dequeue();
    }

    queue.Count.ShouldBe(0);
    dequeueValues
      .ShouldBe([42, 3.14, new TestValue(100), new TestMessage("hello")]);
    modifyCount.ShouldBe(8);
  }

  [Fact]
  public void ConditionalCallbacks()
  {
    var queue = new AutoQueue();
    var enqueueValues = new List<int>();
    var evenEnqueueValues = new List<int>();
    var dequeueValues = new List<int>();
    var evenDequeueValues = new List<int>();
    var modifyCount = 0;

    queue.Bind()
      .OnEnqueue((in int v) => enqueueValues.Add(v))
      .OnEnqueue(
        (in int v) => evenEnqueueValues.Add(v),
        condition: v => v % 2 == 0
      )
      .OnDequeue((in int v) => dequeueValues.Add(v))
      .OnDequeue(
        (in int v) => evenDequeueValues.Add(v),
        condition: v => v % 2 == 0
      )
      .OnModify(() => modifyCount++);

    queue.Enqueue(1);
    queue.Enqueue(2);
    queue.Enqueue(3);
    queue.Enqueue(4);
    queue.Enqueue(5);

    enqueueValues.ShouldBe([1, 2, 3, 4, 5]);
    evenEnqueueValues.ShouldBe([2, 4]);
    dequeueValues.ShouldBe([]);
    evenDequeueValues.ShouldBe([]);
    modifyCount.ShouldBe(5);

    while (queue.HasValues)
    {
      queue.Dequeue();
    }

    dequeueValues.ShouldBe([1, 2, 3, 4, 5]);
    evenDequeueValues.ShouldBe([2, 4]);
    modifyCount.ShouldBe(10);
  }

  [Fact]
  public void DequeueOnEmptyDoesNotBroadcast()
  {
    var queue = new AutoQueue();
    var dequeueValues = new List<object>();
    var modifyCount = 0;

    queue.Bind()
      .OnDequeue((in int v) => dequeueValues.Add(v))
      .OnModify(() => modifyCount++);

    queue.Dequeue();

    queue.Count.ShouldBe(0);
    dequeueValues.ShouldBe([]);
    modifyCount.ShouldBe(0);
  }

  [Fact]
  public void MultipleBindings()
  {
    var queue = new AutoQueue();
    var enqueueValues1 = new List<int>();
    var enqueueValues2 = new List<int>();
    var enqueueValues3 = new List<int>();
    var dequeueValues1 = new List<int>();
    var dequeueValues2 = new List<int>();
    var dequeueValues3 = new List<int>();
    var modifyCount1 = 0;
    var modifyCount2 = 0;
    var modifyCount3 = 0;

    queue.Bind()
      .OnEnqueue((in int v) => enqueueValues1.Add(v))
      .OnDequeue((in int v) => dequeueValues1.Add(v))
      .OnModify(() => modifyCount1++);

    queue.Bind()
      .OnEnqueue((in int v) => enqueueValues2.Add(v))
      .OnDequeue((in int v) => dequeueValues2.Add(v))
      .OnModify(() => modifyCount2++);

    queue.Bind()
      .OnEnqueue((in int v) => enqueueValues3.Add(v))
      .OnDequeue((in int v) => dequeueValues3.Add(v))
      .OnModify(() => modifyCount3++);

    queue.Enqueue(7);
    queue.Enqueue(14);

    enqueueValues1.ShouldBe([7, 14]);
    enqueueValues2.ShouldBe([7, 14]);
    enqueueValues3.ShouldBe([7, 14]);
    dequeueValues1.ShouldBe([]);
    dequeueValues2.ShouldBe([]);
    dequeueValues3.ShouldBe([]);
    modifyCount1.ShouldBe(2);
    modifyCount2.ShouldBe(2);
    modifyCount3.ShouldBe(2);

    while (queue.HasValues)
    {
      queue.Dequeue();
    }

    dequeueValues1.ShouldBe([7, 14]);
    dequeueValues2.ShouldBe([7, 14]);
    dequeueValues3.ShouldBe([7, 14]);
    modifyCount1.ShouldBe(4);
    modifyCount2.ShouldBe(4);
    modifyCount3.ShouldBe(4);
  }

  [Fact]
  public void DiscardBroadcastsOnlyOnNonEmpty()
  {
    var queue = new AutoQueue();
    var enqueueValues = new List<object>();
    var dequeueValues = new List<object>();
    var discardCount = 0;
    var modifyCount = 0;

    queue.Bind()
      .OnEnqueue((in int v) => enqueueValues.Add(v))
      .OnDequeue((in int v) => dequeueValues.Add(v))
      .OnDiscard(count => discardCount += count)
      .OnModify(() => modifyCount++);

    queue.Enqueue(1);
    queue.Enqueue(2);
    queue.Enqueue(3);
    queue.Enqueue(4);
    queue.Enqueue(5);

    queue.Dequeue();  // dequeue 1
    queue.Discard(2); // discard 2, 3

    // dequeue 4, 5
    while (queue.HasValues)
    {
      queue.Dequeue();
    }

    queue.Enqueue(6);
    queue.Discard(3); // discard 6
    queue.Discard(5); // no-op

    enqueueValues.ShouldBe([1, 2, 3, 4, 5, 6]);
    dequeueValues.ShouldBe([1, 4, 5]);
    discardCount.ShouldBe(3);
    modifyCount.ShouldBe(11);
  }

  [Fact]
  public void ClearBroadcastsOnlyOnNonEmpty()
  {
    var queue = new AutoQueue();
    var clearCount = 0;
    var modifyCount = 0;

    var binding = queue.Bind()
      .OnClear(() => clearCount++)
      .OnModify(() => modifyCount++);

    queue.Clear();

    clearCount.ShouldBe(0);
    modifyCount.ShouldBe(0);
    clearCount = 0;
    modifyCount = 0;

    queue.Enqueue(1);
    queue.Enqueue(2);
    queue.Clear();

    clearCount.ShouldBe(1);
    modifyCount.ShouldBe(3);
    clearCount = 0;
    modifyCount = 0;

    queue.Clear();

    clearCount.ShouldBe(0);
    modifyCount.ShouldBe(0);
  }

  [Fact]
  public void DisposedBindingStopsReceiving()
  {
    var queue = new AutoQueue();
    var modifyCount = 0;

    var binding = queue.Bind()
      .OnModify(() => modifyCount++);

    queue.Enqueue(1);
    modifyCount.ShouldBe(1);

    binding.Dispose();

    queue.Enqueue(2);
    modifyCount.ShouldBe(1); // Should not receive the second value
  }

  [Fact]
  public void ClearsBindings()
  {
    var queue = new AutoQueue();
    var modifyCount = 0;

    var binding = queue.Bind()
      .OnModify(() => modifyCount++);

    queue.Enqueue(1);
    queue.Enqueue(2);

    modifyCount.ShouldBe(2);
    modifyCount = 0;

    queue.ClearBindings();
    queue.Enqueue(3);
    modifyCount.ShouldBe(0);
  }

  [Fact]
  public void Disposes()
  {
    var queue = new AutoQueue();

    queue.Dispose();

    Should.Throw<ObjectDisposedException>(() => queue.Enqueue(2));
  }
}
