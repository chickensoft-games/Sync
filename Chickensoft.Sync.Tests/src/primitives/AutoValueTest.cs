namespace Chickensoft.Sync.Tests.Primitives;

using System;
using System.Collections.Generic;
using Chickensoft.Sync.Primitives;
using Shouldly;
using Xunit;

public sealed class AutoValueTest {
  [Fact]
  public void InitialValue() {
    var autoValue = new AutoValue<int>(5);
    autoValue.Value.ShouldBe(5);
  }

  [Fact]
  public void BroadcastsChanges() {
    var autoValue = new AutoValue<int>(1);
    autoValue.Value.ShouldBe(1);

    var log = new List<int>();
    using var binding = autoValue.Bind();

    binding.OnValue(log.Add);

    // binding should have be invoked with the current value at time of registration
    log.ShouldBe([1]);

    autoValue.Value = 2;
    autoValue.Value.ShouldBe(2);
    autoValue.Value = 3;
    autoValue.Value.ShouldBe(3);

    log.ShouldBe([1, 2, 3]);
  }

  [Fact]
  public void BindingRespectsDerivedTypes() {
    var boots = new Dog("Boots");
    var cookie = new Poodle("Cookie");
    var brisket = new Poodle("Brisket");
    var sven = new Cat("Sven");

    var autoValue = new AutoValue<Animal>(boots);
    var log = new List<string>();

    using var binding = autoValue.Bind()
      .OnValue(animal => log.Add($"animal {animal.Name}"))
      .OnValue(
        animal => log.Add($"animal with R name {animal.Name}"),
        condition: (animal) => animal.Name.StartsWith('R'))
      .OnValue<Dog>(dog => log.Add($"dog {dog.Name}"))
      .OnValue<Poodle>((poodle) => log.Add($"poodle {poodle.Name}"))
      .OnValue<Cat>(cat => log.Add($"cat {cat.Name}"))
      .OnValue<Cat>(
        cat => log.Add($"cat with S name {cat.Name}"),
        condition: (cat) => cat.Name.StartsWith('S')
      );

    log.ShouldBe(["animal Boots", "dog Boots"]);
    log.Clear();

    autoValue.Value = cookie;

    log.ShouldBe(["animal Cookie", "dog Cookie", "poodle Cookie"]);
    log.Clear();

    autoValue.Value = brisket;
    log.ShouldBe(["animal Brisket", "dog Brisket", "poodle Brisket"]);
    log.Clear();

    autoValue.Value = sven;
    log.ShouldBe(["animal Sven", "cat Sven", "cat with S name Sven"]);
    log.Clear();

    autoValue.Value = new Dinosaur("Rex");
    log.ShouldBe(["animal Rex", "animal with R name Rex"]);
  }

  [Fact]
  public void ClearsBindings() {
    var autoValue = new AutoValue<int>(1);
    var log = new List<int>();

    using var binding = autoValue.Bind();
    binding.OnValue(log.Add);

    autoValue.Value = 2;
    log.ShouldBe([1, 2]);
    log.Clear();

    autoValue.ClearBindings();
    autoValue.Value = 3;
    log.ShouldBeEmpty();
  }

  [Fact]
  public void DoesNotCallBindingsIfSameValue() {
    var autoValue = new AutoValue<int>(1);
    var log = new List<int>();

    using var binding = autoValue.Bind();
    binding.OnValue(log.Add);

    autoValue.Value = 1;

    log.ShouldBe([1]);
  }

  [Fact]
  public void Disposes() {
    var value = new AutoValue<int>(1);

    value.Dispose();

    Should.Throw<ObjectDisposedException>(() => value.Value = 2);
  }
}
