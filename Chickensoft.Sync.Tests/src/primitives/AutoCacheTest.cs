namespace Chickensoft.Sync.Tests.Primitives;

using Shouldly;
using Sync.Primitives;

public sealed class AutoCacheTest
{
  private readonly record struct TestValue(int Value);
  private sealed record TestRef(int Value);

  [Fact]
  public void Initializes()
  {
    var cache = new AutoCache();
    cache.Count.ShouldBe(0);
    cache.TryGetValue<TestValue>(out _).ShouldBeFalse();
  }

  [Fact]
  public void BroadcastsChanges()
  {
    var cache = new AutoCache();

    var values = new List<object>();

    cache.Bind()
      .OnUpdate((in int v) => values.Add(v))
      .OnUpdate((in double v) => values.Add(v))
      .OnUpdate((string v) => values.Add(v));

    cache.Update(5);
    cache.Update(3.14);
    cache.Update("hello");

    cache.TryGetValue<int>(out var integer).ShouldBeTrue();
    cache.TryGetValue<double>(out var dec).ShouldBeTrue();
    cache.TryGetValue<string>(out var str).ShouldBeTrue();

    integer.ShouldBe(5);
    dec.ShouldBe(3.14);
    str.ShouldBe("hello");

    values.ShouldBe([5, 3.14, "hello"]);
  }

  [Fact]
  public void BindingRespectsDerivedTypes()
  {
    var boots = new Dog("Boots");
    var cookie = new Poodle("Cookie");
    var brisket = new Poodle("Brisket");
    var sven = new Cat("Sven");

    var autoCache = new AutoCache();
    var log = new List<string>();

    using var binding = autoCache.Bind()
      .OnUpdate<Animal>(animal => log.Add($"animal {animal.Name}"))
      .OnUpdate<Animal>(
        animal => log.Add($"animal with R name {animal.Name}"),
        condition: (animal) => animal.Name.StartsWith('R'))
      .OnUpdate<Dog>(dog => log.Add($"dog {dog.Name}"))
      .OnUpdate<Poodle>((poodle) => log.Add($"poodle {poodle.Name}"))
      .OnUpdate<Cat>(cat => log.Add($"cat {cat.Name}"))
      .OnUpdate<Cat>(
        cat => log.Add($"cat with S name {cat.Name}"),
        condition: (cat) => cat.Name.StartsWith('S')
      );

    autoCache.Update(boots);

    log.ShouldBe(["animal Boots", "dog Boots"]);
    log.Clear();

    autoCache.Update(cookie);

    log.ShouldBe(["animal Cookie", "dog Cookie", "poodle Cookie"]);
    log.Clear();

    autoCache.Update(brisket);
    log.ShouldBe(["animal Brisket", "dog Brisket", "poodle Brisket"]);
    log.Clear();

    autoCache.Update(sven);
    log.ShouldBe(["animal Sven", "cat Sven", "cat with S name Sven"]);
    log.Clear();

    autoCache.Update(new Dinosaur("Rex"));
    log.ShouldBe(["animal Rex", "animal with R name Rex"]);
  }

  [Fact]
  public void TryGetValueReturnsFalseWhenCleared()
  {
    var boots = new Dog("Boots");

    var autoCache = new AutoCache();

    autoCache.Update(boots);

    autoCache.TryGetValue<Dog>(out var dog).ShouldBeTrue();
    dog.ShouldBe(boots);

    autoCache.Count.ShouldBe(1);
    autoCache.Clear();
    autoCache.Count.ShouldBe(0);

    autoCache.TryGetValue<Dog>(out var nullDog).ShouldBeFalse();
    nullDog.ShouldBeNull();
  }

  [Fact]
  public void TryGetValueReturnsPushedType()
  {
    var boots = new Dog("Boots");

    var autoCache = new AutoCache();

    autoCache.Update(boots);
    autoCache.TryGetValue<Dog>(out var dog).ShouldBeTrue();
    dog.ShouldBe(boots);

    autoCache.Update<Animal>(boots);
    autoCache.TryGetValue<Animal>(out var animal).ShouldBeTrue();
    animal.ShouldBe(boots);

    autoCache.Count.ShouldBe(2);
  }

  [Fact]
  public void TryGetValueReturnsLatestValue()
  {
    var cache = new AutoCache();

    cache.Update(new TestValue(5));
    cache.Update(new TestValue(10));
    cache.Update(new TestValue(3));

    cache.TryGetValue<TestValue>(out var value).ShouldBeTrue();
    value.Value.ShouldBe(3);
  }

  [Fact]
  public void TryGetValueReturnsLatestRef()
  {
    var autoCache = new AutoCache();

    autoCache.Update(new TestRef(3));
    autoCache.Update(new TestRef(10));
    autoCache.Update(new TestRef(5));

    autoCache.TryGetValue<TestRef>(out var value).ShouldBeTrue();
    value.ShouldNotBeNull();
    value.Value.ShouldBe(5);
    autoCache.Count.ShouldBe(1);
  }

  [Fact]
  public void ClearsBindings()
  {
    var autoCache = new AutoCache();
    var values = new List<object>();

    using var binding = autoCache.Bind();
    binding.OnUpdate((in int v) => values.Add(v));

    autoCache.Update(1);
    autoCache.Update(2);

    autoCache.TryGetValue<int>(out var value).ShouldBeTrue();
    value.ShouldBe(2);

    values.ShouldBe([1, 2]);
    values.Clear();

    autoCache.ClearBindings();
    autoCache.Update(3);
    values.ShouldBeEmpty();
  }

  [Fact]
  public void ClearBroadcasts()
  {
    var autoCache = new AutoCache();

    autoCache.Update(1);
    autoCache.Update(new TestRef(3));

    var log = new List<string>();
    using var binding = autoCache.Bind();

    binding.OnClear(() => log.Add("clear"));

    autoCache.Clear();

    autoCache.Count.ShouldBe(0);
    log.ShouldBe(["clear"]);
  }

  [Fact]
  public void ClearSetsCountToZero()
  {
    var autoCache = new AutoCache();
    var boots = new Dog("Boots");
    var cookie = new Poodle("Cookie");

    autoCache.Update(1);
    autoCache.Update(2.5);
    autoCache.Count.ShouldBe(2);

    autoCache.Clear();
    autoCache.Count.ShouldBe(0);

    autoCache.Update(4);
    autoCache.Update(9.5);
    autoCache.Update(boots);
    autoCache.Update(cookie);
    autoCache.Count.ShouldBe(4);

    autoCache.Clear();
    autoCache.Count.ShouldBe(0);
  }

  [Fact]
  public void Disposes()
  {
    var cache = new AutoCache();

    cache.Dispose();

    Should.Throw<ObjectDisposedException>(() => cache.Update(2));
  }
}
