namespace Chickensoft.Sync.Tests.Primitives;

using Shouldly;
using Utopia;

public sealed class AutoCacheTest {
  private readonly record struct TestValue(int Value);
  private record TestRef(int Value);

  [Fact]
  public void Initializes() {
    var cache = new AutoCache();
    cache.Count.ShouldBe(0);
    cache.TryGetValue<TestValue>(out _).ShouldBeFalse();
  }

  [Fact]
  public void BroadcastsChanges() {
    var cache = new AutoCache();

    var values = new List<object>();

    cache.Bind()
      .OnValue((in int v) => values.Add(v))
      .OnValue((in double v) => values.Add(v))
      .OnValue((string v) => values.Add(v));

    cache.Push(5);
    cache.Push(3.14);
    cache.Push("hello");

    values.ShouldBe([5, 3.14, "hello"]);
  }

  [Fact]
  public void TryGetValueReturnsLatestValue() {
    var cache = new AutoCache();

    cache.Push(new TestValue(5));
    cache.Push(new TestValue(10));
    cache.Push(new TestValue(3));

    cache.TryGetValue<TestValue>(out var value).ShouldBeTrue();
    value.Value.ShouldBe(3);
  }

  [Fact]
  public void TryGetValueReturnsLatestRef() {
    var cache = new AutoCache();

    cache.Push(new TestRef(3));
    cache.Push(new TestRef(10));
    cache.Push(new TestRef(5));

    cache.TryGetValue<TestRef>(out var value).ShouldBeTrue();
    value.ShouldNotBeNull();
    value.Value.ShouldBe(5);
  }

  [Fact]
  public void Disposes() {
    var cache = new AutoCache();

    cache.Dispose();

    Should.Throw<ObjectDisposedException>(() => cache.Push(2));
  }
}
