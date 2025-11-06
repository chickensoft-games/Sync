namespace Chickensoft.Sync.Tests.Primitives;

using System;
using System.Collections;
using System.Collections.Generic;
using Chickensoft.Sync.Primitives;
using Shouldly;
using Xunit;

public sealed class AutoSetTest
{
  [Fact]
  public void Initializes()
  {
    var set = new AutoSet<int>();
    set.Count.ShouldBe(0);
    set.IsReadOnly.ShouldBeFalse();
  }

  [Fact]
  public void InitializesWithItemsAndComparer()
  {
    var set = new AutoSet<string>(
      new HashSet<string> { "one", "two" },
      StringComparer.OrdinalIgnoreCase
    );

    set.Count.ShouldBe(2);
    set.Contains("one").ShouldBeTrue();
    set.Contains("two").ShouldBeTrue();
    set.Contains("ONE").ShouldBeTrue();

    set.Comparer.ShouldBeSameAs(StringComparer.OrdinalIgnoreCase);
  }

  [Fact]
  public void AddBroadcasts()
  {
    var set = new AutoSet<int>();
    var log = new List<string>();
    using var binding = set.Bind();

    binding.OnAdd(item => log.Add($"add {item}"));

    set.Add(1);

    set.Count.ShouldBe(1);
    set.Contains(1).ShouldBeTrue();
    log.ShouldBe(["add 1"]);
  }

  [Fact]
  public void OnRemoveBroadcasts()
  {
    var set = new AutoSet<int> { 1 };
    var log = new List<string>();
    using var binding = set.Bind();

    binding.OnRemove(item => log.Add($"remove {item}"));

    set.Remove(1);

    set.Count.ShouldBe(0);
    set.Contains(1).ShouldBeFalse();
    log.ShouldBe(["remove 1"]);
  }

  [Fact]
  public void ClearBroadcasts()
  {
    var set = new AutoSet<int> { 1, 2, 3 };
    var log = new List<string>();
    using var binding = set.Bind();

    binding.OnClear(() => log.Add("clear"));

    set.Clear();

    set.Count.ShouldBe(0);
    set.Contains(1).ShouldBeFalse();
    set.Contains(2).ShouldBeFalse();
    set.Contains(3).ShouldBeFalse();
    log.ShouldBe(["clear"]);
  }

  [Fact]
  public void CopyTo()
  {
    var set = new AutoSet<int> { 1, 2, 3 };
    var array = new int[5];
    set.CopyTo(array, 1);
    array.ShouldContain(1);
    array.ShouldContain(2);
    array.ShouldContain(3);
  }

  [Fact]
  public void ICollectionRemoveThrows()
  {
    var set = new AutoSet<int> { 1, 2, 3 } as ICollection<int>;
    Should.Throw<NotSupportedException>(() => set.Remove(1));
  }

  [Fact]
  public void ProvidesBoxedEnumerator()
  {
    var set = new AutoSet<int> { 1, 2, 3 } as IEnumerable<int>;
    var enumerator = set.GetEnumerator();
    (set as IEnumerable).GetEnumerator()
      .ShouldBeOfType<HashSet<int>.Enumerator>();

    var items = new List<int>();

    while (enumerator.MoveNext())
    {
      items.Add(enumerator.Current);
    }

    items.Count.ShouldBe(3);
    items.ShouldContain(1);
    items.ShouldContain(2);
    items.ShouldContain(3);
  }

  [Fact]
  public void ClearDoesNotBroadcastWhenEmpty()
  {
    var set = new AutoSet<int>();
    var called = false;
    using var binding = set.Bind();
    binding.OnClear(() => called = true);

    set.Clear();

    called.ShouldBeFalse();
  }

  [Fact]
  public void BindingRespectsDerivedTypes()
  {
    var set = new AutoSet<Animal>();
    var log = new List<string>();

    using var binding = set.Bind();
    binding.OnAdd((Dog dog) => log.Add($"add dog {dog.Name}"));
    binding.OnAdd((Poodle poodle) => log.Add($"add poodle {poodle.Name}"));
    binding.OnRemove((Cat cat) => log.Add($"remove cat {cat.Name}"));
    binding.OnRemove(animal => log.Add($"remove animal {animal.Name}"));

    var pickles = new Cat("Pickles");
    var boots = new Dog("Boots");
    var brisket = new Poodle("Brisket");

    set.Add(pickles);
    set.Add(boots);
    set.Add(brisket);

    set.Remove(boots);
    set.Remove(pickles);
    set.Remove(brisket);

    log.ShouldBe([
      "add dog Boots",
      "add dog Brisket",
      "add poodle Brisket",
      "remove animal Boots",
      "remove cat Pickles",
      "remove animal Pickles",
      "remove animal Brisket",
    ]);

    set.ClearBindings();
    log.Clear();

    set.Add(pickles);

    log.ShouldBeEmpty();
  }

  [Fact]
  public void Disposes()
  {
    var set = new AutoSet<int>();

    set.Dispose();

    Should.Throw<ObjectDisposedException>(() => set.Add(1));
  }
}
