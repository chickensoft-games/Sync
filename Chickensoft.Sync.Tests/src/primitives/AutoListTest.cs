namespace Chickensoft.Sync.Tests.Primitives;

using System;
using System.Collections.Generic;
using Chickensoft.Sync.Primitives;
using Shouldly;
using Xunit;

public sealed class AutoListTest {
  [Fact]
  public void Initializes() {
    var list = new AutoList<int>();
    list.Count.ShouldBe(0);
    list.IsReadOnly.ShouldBeFalse();
  }

  [Fact]
  public void AddBroadcastsItemAndIndex() {
    var list = new AutoList<int>();
    var log = new List<string>();
    using var binding = list.Bind();

    binding.OnAdd((int item, int index) => log.Add($"add {item} @ {index}"));

    list.Add(42);

    list.Count.ShouldBe(1);
    list.IndexOf(42).ShouldBe(0);
    log.ShouldBe(["add 42 @ 0"]);
  }

  [Fact]
  public void InsertBroadcastsCorrectIndex() {
    var list = new AutoList<int>([10, 30]);
    var log = new List<string>();
    using var binding = list.Bind();

    binding.OnAdd((int item, int index) => log.Add($"add {item} @ {index}"));

    list.Insert(1, 20);

    list.ShouldBe([10, 20, 30]);
    log.ShouldBe(["add 20 @ 1"]);
  }

  [Fact]
  public void InsertOutOfBounds() {
    var list = new AutoList<int>([1, 2, 3]);
    Should.Throw<ArgumentOutOfRangeException>(() => list.Insert(-1, 0));
    Should.Throw<ArgumentOutOfRangeException>(() => list.Insert(4, 0));
  }

  [Fact]
  public void IndexerUpdateBroadcasts() {
    var list = new AutoList<string>(["a"]);
    var log = new List<string>();
    using var binding = list.Bind();

    binding.OnUpdate(
      (string prev, string next, int idx) => log.Add($"{prev}->{next} @ {idx}")
    );

    list[0] = "b";

    list[0].ShouldBe("b");
    log.ShouldBe(["a->b @ 0"]);
  }

  [Fact]
  public void IndexerDoesNotUpdateWhenComparerSaysEqual() {
    var list = new AutoList<string>(
      ["A"], comparer: StringComparer.OrdinalIgnoreCase
    );
    var called = false;
    using var binding = list.Bind();

    binding.OnUpdate((string _, string __) => called = true);

    list[0] = "a";

    called.ShouldBeFalse();
    list[0].ShouldBe("A");
  }

  [Fact]
  public void RemoveAt_BroadcastsItemAndIndex() {
    var list = new AutoList<int>([1, 2, 3]);
    var log = new List<string>();
    using var binding = list.Bind();

    binding.OnRemove(
      (int item, int index) => log.Add($"remove {item} @ {index}")
    );

    list.RemoveAt(1);

    list.ShouldBe([1, 3]);
    log.ShouldBe(["remove 2 @ 1"]);
  }

  [Fact]
  public void RemoveOutOfBounds() {
    var list = new AutoList<int>([1]);
    Should.Throw<ArgumentOutOfRangeException>(() => list.RemoveAt(-1));
    Should.Throw<ArgumentOutOfRangeException>(() => list.RemoveAt(1));
  }

  [Fact]
  public void Clear() {
    var list = new AutoList<int>();
    var calls = 0;
    list.Bind().OnClear(() => calls++);

    list.Clear(); // empty -> no-op
    calls.ShouldBe(0);

    list.Add(1);
    list.Clear(); // now non-empty -> broadcast
    calls.ShouldBe(1);
    list.Count.ShouldBe(0);
  }

  [Fact]
  public void ContainsAndIndexOfRespectComparer() {
    var list = new AutoList<string>(
      ["Aardvark", "Bear"], StringComparer.OrdinalIgnoreCase
    );
    list.Contains("bear").ShouldBeTrue();
    list.IndexOf("bear").ShouldBe(1);
  }

  [Fact]
  public void CopyToAndEnumerationPreserveOrder() {
    var list = new AutoList<int>([5, 6, 7]);
    var arr = new int[5];
    list.CopyTo(arr, 1);
    arr.ShouldBe([0, 5, 6, 7, 0]);

    var enumerated = new List<int>();

    foreach (var x in list) {
      enumerated.Add(x);
    }

    enumerated.ShouldBe([5, 6, 7]);
  }

  [Fact]
  public void BindingRespectsDerivedTypes() {
    var list = new AutoList<Animal>();
    var log = new List<string>();
    list.Bind()
      .OnAdd<Dog>(dog => log.Add($"add dog {dog.Name}"))
      .OnAdd<Poodle>((poodle, _) => log.Add($"add poodle {poodle.Name}"))
      .OnAdd<Cat>(cat => log.Add($"add cat {cat.Name}"))
      .OnRemove(animal => log.Add($"remove animal {animal.Name}"))
      .OnRemove<Dog>(dog => log.Add($"remove dog {dog.Name}"))
      .OnRemove<Cat>((cat, i) => log.Add($"remove cat {cat.Name} @ {i}"))
      .OnUpdate(
        (prev, next) => log.Add($"updated an animal {prev.Name}->{next.Name}")
      )
      .OnUpdate<Cat, Dog>(
        (cat, dog) => log.Add($"updated a cat to a dog {cat.Name}->{dog.Name}")
      )
      .OnUpdate<Cat, Cat>(
        (prev, next, i) =>
          log.Add($"updated a cat {prev.Name}->{next.Name} @ {i}")
      );

    var cookie = new Poodle("Cookie");
    var brisket = new Poodle("Brisket");
    var sven1 = new Cat("Sven 1");
    var sven2 = new Cat("Sven 2");
    var boots = new Dog("Boots");
    var garbageCat = new Cat("Garbage Cat");
    var tiger = new Cat("Tiger");
    var pickles = new Cat("Pickles");

    list.Add(cookie);
    list.Add(sven1);
    list.Add(pickles);
    list.Add(garbageCat);
    list.Add(boots);
    list.Add(tiger);
    list.Remove(boots);
    list[1] = sven2;
    list[3] = brisket;
    list.Remove(tiger);

    log.ShouldBe([
      "add dog Cookie",
      "add poodle Cookie",
      "add cat Sven 1",
      "add cat Pickles",
      "add cat Garbage Cat",
      "add dog Boots",
      "add cat Tiger",
      "remove animal Boots",
      "remove dog Boots",
      "updated an animal Sven 1->Sven 2",
      "updated a cat Sven 1->Sven 2 @ 1",
      "updated an animal Garbage Cat->Brisket",
      "updated a cat to a dog Garbage Cat->Brisket",
      "remove animal Tiger",
      "remove cat Tiger @ 4"
    ]);
  }

  [Fact]
  public void OperationsDefer() {
    var list = new AutoList<int>();
    var log = new List<string>();
    using var binding1 = list.Bind();
    binding1.OnAdd((int value) => {
      log.Add($"b1 {value}");
      if (value == 1) {
        list.Add(2); // schedule another one
      }
    });
    using var binding2 = list.Bind();
    binding2.OnAdd((int value) => log.Add($"b2 {value}"));

    list.Add(1);

    log.ShouldBe(["b1 1", "b2 1", "b1 2", "b2 2"]);
  }

  [Fact]
  public void ClearBindingsDefers() {
    var list = new AutoList<int>();
    var log = new List<string>();

    using var binding1 = list.Bind();
    binding1.OnAdd((int value) => {
      log.Add($"b1 {value}");
      if (value == 1) {
        list.ClearBindings(); // should not affect current broadcast
      }
    });
    using var binding2 = list.Bind();
    binding2.OnAdd((int v) => log.Add($"b2 {v}"));

    list.Add(1); // both bindings should see this
    list.Add(2); // no bindings should see this

    log.ShouldBe(["b1 1", "b2 1"]);
  }

  [Fact]
  public void RemoveByItem() {
    var list = new AutoList<int>([1, 2, 3]);
    var log = new List<string>();
    list.Remove(2);

    list.ShouldBe([1, 3]);

    Should.NotThrow(() => list.Remove(42));

    list.ShouldBe([1, 3]);
  }

  [Fact]
  public void UpdateThrowsIfIndexOutOfBounds() {
    var list = new AutoList<int>([1, 2, 3]);
    Should.Throw<ArgumentOutOfRangeException>(() => list[3] = 10);
    Should.Throw<ArgumentOutOfRangeException>(() => list[-1] = 10);
  }

  [Fact]
  public void ICollectionRemoveIsNotSupported() {
    var list = new AutoList<int>([1, 2, 3]);
    var collection = (ICollection<int>)list;
    Should.Throw<NotSupportedException>(() => collection.Remove(2));
  }

  [Fact]
  public void IndexerSetOutOfBoundsThrows() {
    var list = new AutoList<int>();
    Should.Throw<ArgumentOutOfRangeException>(() => list[0] = 10);
  }

  [Fact]
  public void IndexOfReturnsNeg1ForMissingItem() {
    var list = new AutoList<string>(["a", "b", "c"]);
    list.IndexOf("x").ShouldBe(-1);
  }

  [Fact]
  public void ProvidesBoxedEnumerator() {
    IEnumerable<int> list = new AutoList<int>([1, 2, 3]);
    var enumerator = list.GetEnumerator();
    var items = new List<int>();

    while (enumerator.MoveNext()) {
      items.Add(enumerator.Current);
    }

    items.ShouldBe([1, 2, 3]);

    enumerator.ShouldBeOfType<List<int>.Enumerator>();
  }

  [Fact]
  public void Disposes() {
    var list = new AutoList<int>();

    list.Dispose();

    Should.Throw<ObjectDisposedException>(() => list.Add(1));
  }
}
