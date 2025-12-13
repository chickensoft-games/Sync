namespace Chickensoft.Sync.Tests.Primitives;

using System;
using System.Collections;
using System.Collections.Generic;
using Chickensoft.Sync.Primitives;
using Shouldly;
using Xunit;

public sealed class AutoMapTest
{
  [Fact]
  public void Initializes()
  {
    var map = new AutoMap<int, string>();
    map.Count.ShouldBe(0);
    map.IsReadOnly.ShouldBeFalse();
  }

  [Fact]
  public void InitializesWithItemsAndComparers()
  {
    var map = new AutoMap<string, string>(
      new Dictionary<string, string>
      {
        ["1"] = "one",
        ["2"] = "two"
      },
      StringComparer.OrdinalIgnoreCase
    );

    map.Count.ShouldBe(2);
    map["1"].ShouldBe("one");
    map["2"].ShouldBe("two");

    map.Comparer.ShouldBeSameAs(StringComparer.OrdinalIgnoreCase);
  }

  [Fact]
  public void AddBroadcasts()
  {
    var map = new AutoMap<int, string>();
    var log = new List<string>();
    using var binding = map.Bind();

    binding.OnAdd((key, value) => log.Add($"add {key} -> {value}"));

    map.Add(1, "one");

    map.Count.ShouldBe(1);
    map[1].ShouldBe("one");
    log.ShouldBe(["add 1 -> one"]);
  }

  [Fact]
  public void AddBroadcastsModification()
  {
    var map = new AutoMap<int, string>();
    var log = new List<string>();
    using var binding = map.Bind();

    binding.OnModify(() => log.Add($"modify"));

    map.Add(1, "one");

    map.Count.ShouldBe(1);
    map[1].ShouldBe("one");
    log.ShouldBe(["modify"]);
  }

  [Fact]
  public void UpdateBroadcasts()
  {
    var map = new AutoMap<int, string> { [1] = "one" };
    var log = new List<string>();
    using var binding = map.Bind();

    binding.OnUpdate((key, oldValue, newValue) =>
      log.Add($"update {key} : {oldValue} -> {newValue}"));

    map[1] = "uno";

    map.Count.ShouldBe(1);
    map[1].ShouldBe("uno");
    log.ShouldBe(["update 1 : one -> uno"]);
  }

  [Fact]
  public void UpdateBroadcastsModification()
  {
    var map = new AutoMap<int, string> { [1] = "one" };
    var log = new List<string>();
    using var binding = map.Bind();

    binding.OnModify(() => log.Add($"modify"));

    map[1] = "uno";

    map.Count.ShouldBe(1);
    map[1].ShouldBe("uno");
    log.ShouldBe(["modify"]);
  }

  [Fact]
  public void RemoveWithValueBroadcasts()
  {
    var map = new AutoMap<int, string> { [1] = "one" };
    var log = new List<string>();
    using var binding = map.Bind();

    binding.OnRemove(
      (key, value) => log.Add($"remove {key} -> {value}")
    );

    map.Remove(1);

    map.Count.ShouldBe(0);
    log.ShouldBe(["remove 1 -> one"]);
  }

  [Fact]
  public void RemoveWithValueBroadcastsModification()
  {
    var map = new AutoMap<int, string> { [1] = "one" };
    var log = new List<string>();
    using var binding = map.Bind();

    binding.OnModify(() => log.Add($"modify"));

    map.Remove(1);

    map.Count.ShouldBe(0);
    log.ShouldBe(["modify"]);
  }

  [Fact]
  public void RemoveBroadcasts()
  {
    var map = new AutoMap<int, string> { [1] = "one" };
    var log = new List<string>();
    using var binding = map.Bind();

    binding.OnRemove(key => log.Add($"remove {key}"));

    map.Remove(1);

    map.Count.ShouldBe(0);
    log.ShouldBe(["remove 1"]);
  }

  [Fact]
  public void RemoveBroadcastsModification()
  {
    var map = new AutoMap<int, string> { [1] = "one" };
    var log = new List<string>();
    using var binding = map.Bind();

    binding.OnModify(() => log.Add($"modify"));

    map.Remove(1);

    map.Count.ShouldBe(0);
    log.ShouldBe(["modify"]);
  }

  [Fact]
  public void ClearBroadcastsOnlyIfNotEmpty()
  {
    var map = new AutoMap<int, string>();
    var log = new List<string>();
    using var binding = map.Bind();

    binding.OnClear(() => log.Add("clear"));

    map.Clear();
    log.ShouldBeEmpty();

    map.Add(1, "one");
    map.Add(2, "two");

    map.Clear();
    map.Count.ShouldBe(0);
    log.ShouldBe(["clear"]);
  }

  [Fact]
  public void ClearBroadcastsModificationOnlyIfNotEmpty()
  {
    var map = new AutoMap<int, string>();
    var log = new List<string>();
    using var binding = map.Bind();

    binding.OnModify(() => log.Add($"modify"));

    map.Clear();

    map.Add(1, "one");
    map.Add(2, "two");

    map.Clear();
    map.Count.ShouldBe(0);
    // 2 for the adds, 1 for the clear
    log.ShouldBe(["modify", "modify", "modify"]);
  }

  [Fact]
  public void ProvidesBoxedEnumerableAndCollections()
  {
    var map = new AutoMap<int, string>();
    var mDict = map as IDictionary<int, string>;
    var rDict = map as IReadOnlyDictionary<int, string>;

    Should.NotThrow(() => rDict.Keys);
    Should.NotThrow(() => rDict.Values);
    Should.NotThrow(() => mDict.Keys);
    Should.NotThrow(() => mDict.Values);
  }

  [Fact]
  public void KeysEnumerator()
  {
    var map = new AutoMap<int, string> { [1] = "one", [2] = "two" };
    var keys = new List<int>();

    foreach (var key in map.Keys)
    {
      keys.Add(key);
    }

    keys.Count.ShouldBe(2);
    keys.ShouldContain(1);
    keys.ShouldContain(2);
  }

  [Fact]
  public void ValuesEnumerator()
  {
    var map = new AutoMap<int, string> { [1] = "one", [2] = "two" };
    var values = new List<string>();

    foreach (var value in map.Values)
    {
      values.Add(value);
    }

    values.Count.ShouldBe(2);
    values.ShouldContain("one");
    values.ShouldContain("two");
  }

  [Fact]
  public void AddsAndRemovesAndClearsBindings()
  {
    var map = new AutoMap<int, string>();
    var log = new List<string>();

    var b1 = map.Bind();
    var b2 = map.Bind();
    var b3 = map.Bind();

    b1.OnAdd((key, value) => log.Add($"b1 add {key} -> {value}"));
    b2.OnAdd((key, value) => log.Add($"b2 add {key} -> {value}"));
    b3.OnAdd((key, value) => log.Add($"b3 add {key} -> {value}"));

    b2.Dispose(); // removes it

    map.Add(1, "one");

    log.ShouldBe(["b1 add 1 -> one", "b3 add 1 -> one"]);

    map.ClearBindings();
    log.Clear();

    map.Add(2, "two");
    log.ShouldBe([]);
  }

  [Fact]
  public void ContainsKey()
  {
    var map = new AutoMap<int, string> { [1] = "one", [2] = "two" };
    map.ContainsKey(1).ShouldBeTrue();
    map.ContainsKey(2).ShouldBeTrue();
    map.ContainsKey(3).ShouldBeFalse();
  }

  [Fact]
  public void TryGetValue()
  {
    var map = new AutoMap<int, string> { [1] = "one", [2] = "two" };
    map.TryGetValue(1, out var value1).ShouldBeTrue();
    value1.ShouldBe("one");
    map.TryGetValue(2, out var value2).ShouldBeTrue();
    value2.ShouldBe("two");
    map.TryGetValue(3, out var value3).ShouldBeFalse();
    value3.ShouldBeNull();
  }

  [Fact]
  public void Contains()
  {
    var map = new AutoMap<int, string> { [1] = "one", [2] = "two" };
    map.Contains(new KeyValuePair<int, string>(1, "one")).ShouldBeTrue();
    map.Contains(new KeyValuePair<int, string>(2, "two")).ShouldBeTrue();
    map.Contains(new KeyValuePair<int, string>(3, "three")).ShouldBeFalse();
  }

  [Fact]
  public void AddKvp()
  {
    var map = new AutoMap<int, string>();

    map.Count.ShouldBe(0);

    map.Add(new KeyValuePair<int, string>(1, "one"));
    map.Add(new KeyValuePair<int, string>(2, "two"));

    map.Count.ShouldBe(2);

    map[1].ShouldBe("one");
    map[2].ShouldBe("two");
  }

  [Fact]
  public void RemoveKvp()
  {
    var map = new AutoMap<int, string> { [1] = "one", [2] = "two" };
    var log = new List<string>();

    using var binding = map.Bind();
    binding.OnRemove(
      (key, value) => log.Add($"remove {key} -> {value}")
    );

    map.Remove(new KeyValuePair<int, string>(1, "one"));
    map.Count.ShouldBe(1);
    log.ShouldBe(["remove 1 -> one"]);

    log.Clear();
    Should.NotThrow(
      () => map.Remove(new KeyValuePair<int, string>(2, "three"))
    );
    map.Count.ShouldBe(1);
    log.ShouldBe([]);
  }

  [Fact]
  public void AddUpdatesIfAlreadyExists()
  {
    var map = new AutoMap<int, string> { [1] = "one" };
    map.Add(1, "uno");
    map.Count.ShouldBe(1);
    map[1].ShouldBe("uno");
  }

  [Fact]
  public void RemoveDoesNotBroadcastIfKeyDoesNotExist()
  {
    var map = new AutoMap<int, string> { [1] = "one" };
    var called = false;

    using var binding = map.Bind();
    binding.OnRemove((_, __) => called = true);

    map.Remove(2);

    called.ShouldBeFalse();
  }

  [Fact]
  public void RemoveMatchingDoesNotBroadcastIfKeyDoesNotExist()
  {
    var map = new AutoMap<int, string> { [1] = "one" };
    var called = false;

    using var binding = map.Bind();
    binding.OnRemove((_, __) => called = true);

    map.Remove(new KeyValuePair<int, string>(2, "two"));

    called.ShouldBeFalse();
  }

  [Fact]
  public void ClearDoesNotBroadcastIfEmpty()
  {
    var map = new AutoMap<int, string>();
    var called = false;

    using var binding = map.Bind();
    binding.OnClear(() => called = true);

    map.Clear();

    called.ShouldBeFalse();
  }

  [Fact]
  public void CopyTo()
  {
    var map = new AutoMap<int, string> { [1] = "one", [2] = "two" };
    var array = new KeyValuePair<int, string>[2];

    map.CopyTo(array, 0);

    array.ShouldContain(new KeyValuePair<int, string>(1, "one"));
    array.ShouldContain(new KeyValuePair<int, string>(2, "two"));
  }

  [Fact]
  public void ProvidesBoxedEnumerator()
  {
    IEnumerable<KeyValuePair<int, string>> map =
      new AutoMap<int, string> { [1] = "one", [2] = "two", [3] = "three" };
    var enumerator = map.GetEnumerator();
    var items = new List<KeyValuePair<int, string>>();

    while (enumerator.MoveNext())
    {
      items.Add(enumerator.Current);
    }

    items.ShouldContain(new KeyValuePair<int, string>(1, "one"));
    items.ShouldContain(new KeyValuePair<int, string>(2, "two"));
    items.ShouldContain(new KeyValuePair<int, string>(3, "three"));

    enumerator.ShouldBeOfType<Dictionary<int, string>.Enumerator>();

    IEnumerable nonGenericMap = map;
    var nonGenericEnumerator = nonGenericMap.GetEnumerator();

    nonGenericEnumerator.ShouldBeOfType<Dictionary<int, string>.Enumerator>();
  }

  [Fact]
  public void IDictionaryRemoveKeyIsNotSupported()
  {
    IDictionary<int, string> map = new AutoMap<int, string>
    {
      [1] = "one",
      [2] = "two"
    };

    Should.Throw<NotSupportedException>(() => map.Remove(1));
  }

  [Fact]
  public void ICollectionRemoveKvpIsNotSupported()
  {
    ICollection<KeyValuePair<int, string>> map = new AutoMap<int, string>
    {
      [1] = "one",
      [2] = "two"
    };
    Should.Throw<NotSupportedException>(
      () => map.Remove(new KeyValuePair<int, string>(1, "one"))
    );
  }

  [Fact]
  public void Disposes()
  {
    var map = new AutoMap<int, string>();

    map.Dispose();

    Should.Throw<ObjectDisposedException>(() => map.Add(1, "one"));
  }

  [Fact]
  public void Enumerates()
  {
    var map = new AutoMap<int, string> { [1] = "one", [2] = "two" };
    var items = new Dictionary<int, string>();

    foreach (var kvp in map)
    {
      items.Add(kvp.Key, kvp.Value);
    }

    items[1].ShouldBe("one");
    items[2].ShouldBe("two");
  }

  [Fact]
  public void KeyEnumeratorForwards()
  {
    var map = new AutoMap<int, string> { [1] = "one", [2] = "two" };
    var enumerator = map.Keys;

    enumerator.MoveNext().ShouldBeTrue();
    enumerator.Current.ShouldBeOfType<int>();
    (enumerator as IEnumerator).Current.ShouldBeOfType<int>();
    enumerator.MoveNext().ShouldBeTrue();
    enumerator.Current.ShouldBeOfType<int>();
    enumerator.MoveNext().ShouldBeFalse();

    Should.NotThrow(enumerator.Reset);
    Should.NotThrow(enumerator.Dispose);
  }

  [Fact]
  public void ValueEnumeratorForwards()
  {
    var map = new AutoMap<int, string> { [1] = "one", [2] = "two" };
    var enumerator = map.Values;

    enumerator.MoveNext().ShouldBeTrue();
    enumerator.Current.ShouldBeOfType<string>();
    (enumerator as IEnumerator).Current.ShouldBeOfType<string>();
    enumerator.MoveNext().ShouldBeTrue();
    enumerator.Current.ShouldBeOfType<string>();
    enumerator.MoveNext().ShouldBeFalse();

    Should.NotThrow(enumerator.Reset);
    Should.NotThrow(enumerator.Dispose);
  }
}
