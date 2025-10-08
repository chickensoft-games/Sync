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

    cache.TryGetValue<int>(out var integer).ShouldBeTrue();
    cache.TryGetValue<double>(out var dec).ShouldBeTrue();
    cache.TryGetValue<string>(out var str).ShouldBeTrue();

    integer.ShouldBe(5);
    dec.ShouldBe(3.14);
    str.ShouldBe("hello");

    values.ShouldBe([5, 3.14, "hello"]);
  }

  [Fact]
  public void BindingRespectsDerivedTypes() {
    var boots = new Dog("Boots");
    var cookie = new Poodle("Cookie");
    var brisket = new Poodle("Brisket");
    var sven = new Cat("Sven");

    var autoCache = new AutoCache();
    var log = new List<string>();

    using var binding = autoCache.Bind()
      .OnValue<Animal>(animal => log.Add($"animal {animal.Name}"))
      .OnValue<Animal>(
        animal => log.Add($"animal with R name {animal.Name}"),
        condition: (animal) => animal.Name.StartsWith('R'))
      .OnValue<Dog>(dog => log.Add($"dog {dog.Name}"))
      .OnValue<Poodle>((poodle) => log.Add($"poodle {poodle.Name}"))
      .OnValue<Cat>(cat => log.Add($"cat {cat.Name}"))
      .OnValue<Cat>(
        cat => log.Add($"cat with S name {cat.Name}"),
        condition: (cat) => cat.Name.StartsWith('S')
      );

    autoCache.Push(boots);

    log.ShouldBe(["animal Boots", "dog Boots"]);
    log.Clear();

    autoCache.Push(cookie);

    log.ShouldBe(["animal Cookie", "dog Cookie", "poodle Cookie"]);
    log.Clear();

    autoCache.Push(brisket);
    log.ShouldBe(["animal Brisket", "dog Brisket", "poodle Brisket"]);
    log.Clear();

    autoCache.Push(sven);
    log.ShouldBe(["animal Sven", "cat Sven", "cat with S name Sven"]);
    log.Clear();

    autoCache.Push(new Dinosaur("Rex"));
    log.ShouldBe(["animal Rex", "animal with R name Rex"]);
  }

  [Fact]
  public void TryGetValueReturnsFalseWhenCleared() {
    var boots = new Dog("Boots");

    var autoCache = new AutoCache();

    autoCache.Push(boots);

    autoCache.TryGetValue<Dog>(out var dog).ShouldBeTrue();
    dog.ShouldBe(boots);

    autoCache.Count.ShouldBe(1);
    autoCache.Clear();
    autoCache.Count.ShouldBe(0);

    autoCache.TryGetValue<Dog>(out var nullDog).ShouldBeFalse();
    nullDog.ShouldBeNull();
  }

  [Fact]
  public void TryGetValueReturnsPushedType() {
    var boots = new Dog("Boots");

    var autoCache = new AutoCache();

    autoCache.Push(boots);
    autoCache.TryGetValue<Dog>(out var dog).ShouldBeTrue();
    dog.ShouldBe(boots);

    autoCache.Push<Animal>(boots);
    autoCache.TryGetValue<Animal>(out var animal).ShouldBeTrue();
    animal.ShouldBe(boots);

    autoCache.Count.ShouldBe(2);
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
  public void ClearsBindings() {
    var autoCache = new AutoCache();
    var values = new List<object>();

    using var binding = autoCache.Bind();
    binding.OnValue((in int v) => values.Add(v));

    autoCache.Push(1);
    autoCache.Push(2);

    autoCache.TryGetValue<int>(out var value).ShouldBeTrue();
    value.ShouldBe(2);

    values.ShouldBe([1, 2]);
    values.Clear();

    autoCache.ClearBindings();
    autoCache.Push(3);
    values.ShouldBeEmpty();
  }

  [Fact]
  public void Disposes() {
    var cache = new AutoCache();

    cache.Dispose();

    Should.Throw<ObjectDisposedException>(() => cache.Push(2));
  }
}
