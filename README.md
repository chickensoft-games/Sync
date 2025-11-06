# ⚡️ Sync

[![Chickensoft Badge][chickensoft-badge]][chickensoft-website] [![Discord][discord-badge]][discord] [![Read the docs][read-the-docs-badge]][docs] ![line coverage][line-coverage] ![branch coverage][branch-coverage]

Simple, synchronous, single-threaded reactive programming primitives and collections with fluent bindings. Sync guarantees deterministic execution and defers mutations when executing bindings, protecting your code from [reentrancy] issues.

---

<p align="center">
<img alt="Chickensoft.Sync" src="Chickensoft.Sync/icon.png" width="200">
</p>

---

Sync enforces correctness by default, minimizes memory allocations, and simplifies creating new reactive primitives composed of atomic operations.

Sync is a C# library that works everywhere `netstandard2.1` works.

## ⭐️ Features

- ✅ Simplified terminology tailored for game development use cases.
- ✅ Avoids boxing value types and minimizes heap allocations to reduce garbage collector pressure (suitable for games).
- ✅ Includes observable collections such as `AutoList<T>`, `AutoSet<T>`, and `AutoMap<TKey, TValue>` which are built on top of .NET's standard collection types.
- ✅ Provides an observable property/value (or `BehaviorSubject` in [ReactiveX](https://reactivex.io/documentation/subject.html) terminology) called `AutoValue<T>`.
- ✅ Errors stop execution immediately, same as ordinary C# code.
- ✅ Consistent, fluent bindings tailored for each reactive primitive.
- ✅ Dispose of bindings to unsubscribe from notifications.
- 🤩 *Easily build your own synchronous reactive primitives and collections composed of atomic operations* and notify listeners without having to worry about [reentrancy].

> [!TIP]
> Reactive primitives are synchronous event loops which use a few tricks to essentially eliminate heap allocations in performance critical hot paths.

## 📖 Example Usage

Here's a very simple, real-world game development example that shows how to idiomatically use Sync's `AutoValue<T>` to synchronize an Enemy's visual representation with its underlying model.

> [!NOTE]
> The `AutoValue<int>` and the binding to it `AutoValue<int>.Binding` need to be cleaned up when you're finished to avoid memory leaks.

```csharp
// Enemy gameplay logic
public sealed class Enemy : IDisposable
{
  // mutable observable value private to this class
  private readonly AutoValue<int> _health = new(100);

  // immutable view of the value for outside subscribers
  public IAutoValue<int> Health => _health;

  public void TakeDamage(int damage)
  {
    // enemy can't take more damage than it has health
    var appliedDamage = Math.Min(Math.Abs(damage), _health.Value);
    // bindings will be notified when this goes into effect
    _health.Value -= appliedDamage;
  }

  public void Dispose()
  {
    // release references to any bindings to the health value so they can be GC'd
    _health.Dispose();
  }
}

// Enemy visualization logic
public sealed class EnemyView : IDisposable
{
  public Enemy Enemy { get; }
  public AutoValue<int>.Binding Binding { get; }

  public EnemyView(Enemy enemy)
  {
    Enemy = enemy;
    // listen to changes in the enemy's health
    Binding = enemy.Health.Bind();
    Binding.OnValue(OnHealthChanged);
  }

  public void OnHealthChanged(int health)
  {
    // update the health bar UI, etc.
  }

  public void Dispose()
  {
    Binding.Dispose(); // stop listening
  }
}
```

> [!TIP]
> By convention, objects which own the reactive primitive — the `_health` field in this example — retain a reference to the primitive itself and expose it publicly as a read-only reference that can be used to bind to it.
>
> ```csharp
> private readonly AutoValue<int> _health = new(100); // private mutable view
> public IAutoValue<int> Health => _health; // public read-only view
> ```

Sync has a few more features — we'll document the available APIs along with tips and tricks below.

## 📦 Installation

Sync is available on [nuget].

```sh
dotnet add package Chickensoft.Sync
```

## 🔂 AutoValue

`AutoValue<T>` stores a single value and will broadcast it immediately to any binding callbacks at registration to keep them synchronized. Bindings are notified of any changes to the value for as long as they remain subscribed.

```csharp
// hang onto the value for as long as you want to change it, then call Dispose()
// when you're done with it
var autoValue = new AutoValue<Animal>(new Cat("Pickles"));

// hang onto the binding for as long as you want to observe, then call Dispose()
// on it
using var binding = autoValue.Bind();

// you can chain binding callback registration for ease-of-use
binding
  // called whenever the value changes
  .OnValue(animal => Console.WriteLine($"Observing animal {animal}"))
  // only called for Dog values
  .OnValue((Dog dog) => Console.WriteLine($"Observing dog {dog.Name}"))
  // only called for Cat values
  .OnValue((Cat cat) => Console.WriteLine($"Observing cat {cat.Name}"));

autoValue.Value = new Dog("Brisket");
// Observing animal Brisket
// Observing dog Brisket
autoValue.Value = new Cat("Chibi");
// Observing animal Chibi
// Observing cat Chibi
```

Note that `AutoValue<T>` allows you to register type-specific callbacks for subtypes of `T` (like `Dog` and `Cat` above). For reference types, this makes for some *very* clean code. Don't use it with value types unless you're okay with them [getting boxed][boxing].

```csharp
binding
  // only observe dog values
  .OnValue((Dog dog) => Console.WriteLine($"Observing dog {dog.Name}"))
  // or if you'd rather specify the type as the generic argument instead of as
  // the lambda argument
  .OnValue<Dog>(dog => Console.WriteLine($"Observing dog {dog.Name}"))
```

AutoValue also allows you to provide a predicate to further customize which values you're interested in.

```csharp
binding.OnValue(
  (Dog dog) => Console.WriteLine($"Observing dog with B name {dog.Name}"),
  condition: dog => dog.Name.StartsWith('B') // customize what you care about
);
```

## 🔢 AutoList

`AutoList<T>` is a reactive wrapper around `List<T>`. Bindings will be notified of any changes to the list for as long as they remain subscribed. `AutoList<T>` implements the various `IList<T>` interfaces, so you can generally use it just like a C# list.

```csharp
var autoList = new AutoList<Animal>(
  [
    new Cat("Pickles"),
    new Dog("Cookie"),
    new Dog("Brisket"),
    new Cat("Sven")
  ]
);

using var binding = autoList.Bind();

binding
  .OnAdd(animal => Console.WriteLine($"Animal added: {animal}"))
  // or with its index
  .OnAdd((index, animal) =>
    Console.WriteLine($"Animal added at index {index}: {animal}")
  )
  .OnClear(() => Console.WriteLine("List cleared"))
  // only called when a Dog is added
  .OnAdd((Dog dog) => Console.WriteLine($"Dog added: {dog.Name}"))
  // only called when a Cat is removed
  .OnRemove((Cat cat) => Console.WriteLine($"Cat removed: {cat.Name}"))
  .OnUpdate(
    (previous, current) =>
      Console.WriteLine($"Animal updated from {previous} to {current}")
  )
  .OnUpdate(
    (Dog previous, Dog current) =>
    Console.WriteLine($"Dog updated from {previous.Name} to {current.Name}")
  )
  .OnUpdate(
    (Dog previous, Cat current) =>
    Console.WriteLine($"Dog {previous.Name} replaced by Cat {current.Name}")
  )
  // or with its index
  .OnUpdate((Dog previous, Cat current, int index) =>
    Console.WriteLine(
      $"Dog at index {index} updated from {previous} to Cat {current}"
    )
  );

autoList.Add(new Dog("Chibi"));
autoList.RemoveAt(0);
```

Other method overloads are available for various subtypes, **and each callback can optionally receive the index of the item that was changed**. You can also provide a custom comparer in the constructor.

```csharp
var autoListWithComparer = new AutoList<Animal>([], new MyAnimalComparer());
```

## 🧦 AutoSet<T>

Sometimes, you don't care about tracking a list of things by index. `AutoSet<T>` is a simple reactive wrapper around `HashSet<T>`.

> [!NOTE]
> Due to memory allocation considerations, `AutoSet<T>` does not implement the full `ISet<T>` interfaces, which would require temporary collections to be created to track the result of batch operations.

Bindings will be notified of any changes to the set for as long as they remain subscribed.

```csharp
var autoSet = new AutoSet<Animal>(new HashSet<Animal>
{
  new Cat("Pickles"),
  new Dog("Cookie"),
  new Dog("Brisket"),
  new Cat("Sven")
});

using var binding = autoSet.Bind();

binding
  .OnAdd(animal => Console.WriteLine($"Animal added: {animal}"))
  .OnRemove(animal => Console.WriteLine($"Animal removed: {animal}"))
  // only called when a Dog is added
  .OnAdd((Dog dog) => Console.WriteLine($"Dog added: {dog.Name}"))
  // only called when a Cat is removed
  .OnRemove((Cat cat) => Console.WriteLine($"Cat removed: {cat.Name}"))
  .OnClear(() => Console.WriteLine("Set cleared"));

autoSet.Add(new Dog("Chibi"));
autoSet.Remove(new Cat("Pickles"));
```

## 🗺️ AutoMap

`AutoMap<TKey, TValue>` is a reactive wrapper around `Dictionary<TKey, TValue>`. Bindings will be notified of any changes to the dictionary for as long as they remain subscribed. `AutoMap<TKey, TValue>` implements the various `IDictionary<TKey, TValue>` interfaces, so you can generally use it just like a C# dictionary.

```csharp
var autoMap = new AutoMap<string, Animal>(new Dictionary<string, Animal>
{
  ["Pickles"] = new Cat("Pickles"),
  ["Cookie"] = new Dog("Cookie"),
  ["Brisket"] = new Dog("Brisket"),
  ["Sven"] = new Cat("Sven")
});

using var binding = autoMap.Bind();

binding
  .OnAdd(
    (key, animal) => Console.WriteLine($"Animal added: {key} -> {animal}")
  )
  .OnRemove((key, animal) =>
    Console.WriteLine($"Animal removed: {key} -> {animal}")
  )
  .OnUpdate((key, previous, current) =>
    Console.WriteLine($"Animal updated: {key} from {previous} to {current}")
  )
  .OnClear(() => Console.WriteLine("Map cleared"));

autoMap["Chibi"] = new Dog("Chibi");
autoMap.Remove("Pickles");
autoMap["Brisket"] = new Poodle("Brisket");
```

## 💰 AutoCache

`AutoCache` is a cache which stores values separated by type. On update, it broadcasts to all bindings and stores the
value based on the type given. This can then be retrieved by using the `TryGetValue<T>(out T value)` to get the last value
updated of type `T`. Since `AutoCache` doesn't have a generic param, it is especially useful as a message channel, or a lookup-cache
for multiple types of data. We've optimized `AutoCache` for value types so that it does not box value types on updates.
You might find this pattern familiar if you've used `Chickensoft.LogicBlocks`.

> [!CAUTION]
> When pushing a value of type `Dog` which derives from `Animal`, `TryGetValue<Animal>()` will not return the last value
> updated of type `Dog`. If you desire to get the last `Animal` value updated, you will have to use `Update<Animal>(new Dog())`
> instead. Although Binding notifications for `OnUpdate<Dog>` or `OnUpdate<Animal>` will still be called regardless of the type pushed.

> [!NOTE]
> While `AutoCache` does support reference types, consider using value types instead when initializing new instances on
> update to avoid allocating unnecessary memory that would need to be immediately collected by the garbage collector.
> Using value types where possible helps avoid stuttering and hitches by reducing the amount of work that the garbage
> collector needs to do to clean up reference types on the heap.

```csharp
readonly record struct UpdateName(string DogName);

var autoCache = new AutoCache();
using var binding = autoCache.Bind();

binding
  .OnUpdate<UpdateName>(
    (name) => Console.WriteLine($"Name Updated: {name}")
  )

autoCache.Update(new UpdateName("Pickles"));
autoCache.Update(new UpdateName("Sven"));
// After each update, the OnUpdate callback will be called.

if(autoCache.TryGetValue<UpdateName>(out var update))
{
  // This would print out "Last received dog name: Sven"
  Console.WriteLine($"Last received dog name: {update.DogName}"
}

binding
  .OnUpdate<Animal>(
    (animal) => Console.WriteLine($"Animal Updated: {animal.Name}")
  );

// Store and broadcast a Mouse by its less-specific supertype, Animal
autoCache.Update<Animal>(new Mouse("Hamtaro"));
autoCache.Update(new Dog("Cookie"));
autoCache.Update(new Cat("Pickles"));
// OnUpdate<Animal> will be called 3 times.

//See the caution note above for more information
autoCache.TryGetValue<Animal>(out var animal) // animal will be the Mouse - Hamtaro
autoCache.TryGetValue<Dog>(out var dog) // animal will be the Dog - Cookie
autoCache.TryGetValue<Cat>(out var cat) // animal will be the Cat - Pickles
```

## 🧰 Build Your Own Reactive Primitives

Sync primitives are all built on top of a `SyncSubject`. A `SyncSubject` is an object which your own reactive primitive will own and use to notify `SyncBinding`s of changes in your reactive primitive.

You will have to provide your own `SyncBinding` subclass that's tailored to your reactive primitive. Bespoke bindings for each primitive are what makes Sync's API so pleasant to use, and Sync makes it really easy to create a customized binding.

### Stubbing it Out

Let's build our own implementation of `AutoValue<T>`.

First, we'll want a read-only interface for our reactive primitive. All we need to do is inherit from `IAutoObject<TBinding>`, where `TBinding` is the type of binding we'll create for our AutoValue. We can stub that out, too.

```csharp
public interface IAutoValue<T> : IAutoObject<AutoValue<T>.Binding>
{
  T Value { get; }
}

public sealed class AutoValue<T> : IAutoValue<T>
{
  public class Binding : SyncBinding {
    internal Binding(ISyncSubject subject) : base(subject) { }
  }
}
```

> [!TIP]
> By convention, we nest the binding in the reactive primitive class itself so that it can access private members of the primitive, as well as any of their generic type parameters.

### Atomic Operations

Let's go ahead and implement the required methods for the `IAutoObject` interface. Luckily, we can just forward these to a private `SyncSubject` which handles the deferred event loop system for us. We'll also tell our subject to perform an atomic operation whenever the value is changed, rather than mutating the state right away.

> [!NOTE]
> Later, we'll implement a method that allows us to know when it's time to actually change the value. This is how `SyncSubject` is able to protect us from [reentrancy] issues.

You can define an atomic operation by creating a value type struct. It's really easy to use a one-line `readonly record struct` in C# for this, so that's what we'll do.

```csharp
public sealed class AutoValue<T> : IAutoValue<T>
{
    // Atomic operations
  private readonly record struct UpdateOp(T Value);

  // ... binding class

  private T _value;
  private readonly SyncSubject _subject;

  public T Value {
    get => _value;
    set => _subject.Perform(new UpdateOp(value));
  }

  public AutoValue(T value) {
    _value = value;
    // create a new sync subject that will notify us when it's time to perform
    // the atomic operations we schedule
    _subject = new SyncSubject(this);
  }

  public Binding Bind() => new Binding(_subject);
  public void ClearBindings() => _subject.ClearBindings();
  public void Dispose() => _subject.Dispose();
}
```

### Performing an Atomic Operation

To actually perform our `UpdateOp` operation, we'll edit our AutoValue to implement `IPerform<TOp>` for every atomic operation we want to support. Our AutoValue implementation is really simple, so it's just the one atomic operation for now.

While we're at it, we'll go ahead and create a *broadcast*. A broadcast is also a value type that is sent to each binding. Atomic operations and broadcasts will often be identical, but not always. It's important to keep them distinct.

```csharp
public sealed class AutoValue<T> : IAutoValue<T>,
    IPerform<AutoValue<T>.UpdateOp>
{
  // Atomic operations
  private readonly record struct UpdateOp(T Value);

  // Broadcasts
  public readonly record struct UpdateBroadcast(T Value);

  // ... binding class

  // other members

  // Actually perform the atomic operation
  void IPerform<UpdateOp>.Perform(in UpdateOp op)
  {
    if (_value != op.Value) {
      // only update if it's different
      return;
    }

    _value = op.Value;

    // announce change to relevant binding callbacks
    _subject.Broadcast(new UpdateBroadcast(op.Value));
  }
}
```

### Binding Implementation

Now, the only thing left to do is make our `Binding` class allow the developer to register a callback whenever the value changes.

```csharp
public sealed class AutoValue<T> : IAutoValue<T>,
    IPerform<AutoValue<T>.UpdateOp>,
    IPerform<AutoValue<T>.SyncOp>
{
  // Atomic operations
  private readonly record struct UpdateOp(T Value);
  private readonly record struct SyncOp(Action<T> Callback);

  // Broadcasts
  public readonly record struct UpdateBroadcast(T Value);

  public class Binding : SyncBinding
  {
    internal Binding(ISyncSubject subject) : base(subject) { }

    public Binding OnValue(Action<T> callback)
    {
      AddCallback((in UpdateBroadcast broadcast) => callback(broadcast.Value));

      // invoke binding as soon as possible after it's added to give it the
      // current value immediately. different reactive primitives may or may not
      // want to do this, depending on their desired behavior.
      _subject!.Perform(new SyncOp(callback));

      return this; // to let the developer chain callback registration
    }
  }

  // ... other members shown above

  // Perform the "sync" operation to invoke a callback with the current value
  // when a binding is first added. This mimics a ReactiveX BehaviorSubject.
  void IPerform<SyncOp>.Perform(in SyncOp op) => op.Callback(_value);
}
```

Now, anyone can easily create an auto value and bind to it!

```csharp
var autoValue = new AutoValue<int>(42);
using var binding = autoValue.Bind();
binding.OnValue(value => Console.WriteLine($"Value changed to {value}"));
```

> [!NOTE]
> The [actual `AutoValue<T>` implementation](./Chickensoft.Sync//src/primitives/AutoValue.cs) has to account for a custom comparer, conditional bindings, and derived types, but it's otherwise almost identical.
>
> If you're building your own reactive primitives, take a look at the full source code for `AutoValue<T>`, `AutoList<T>`, `AutoSet<T>`, and `AutoMap<TKey, TValue>` for more examples.

## 🙋‍♀️ Why?

Sync is a generalization of the Chickensoft bindings system first seen in [LogicBlocks]. If you've ever used LogicBlocks, you already know how to use Sync!

### 🐣 Simple

Existing .NET reactive programming libraries are stunted by the reigning naming terminologies: either by trying to conform to ReactiveX's loosely defined terminology or .NET's own poorly-named observer APIs. Neither were designed with game development as the primary use case, and both result in poor code readability or correctness for many typical use cases.

Additionally, [many find Rx.NET just plain confusing and difficult to deal with][rx-confusing].

Not convinced? See how ReactiveX describes its own terminology:

> Each language-specific implementation of ReactiveX has its own naming quirks. There is no canonical naming standard, though there are many commonalities between implementations.
>
> Furthermore, some of these names have different implications in other contexts, or seem awkward in the idiom of a particular implementing language.
>
> For example there is the onEvent naming pattern (e.g. onNext, onCompleted, onError). In some contexts such names would indicate methods by means of which event handlers are registered. In ReactiveX, however, they name the event handlers themselves. - [ReactiveX Docs](https://reactivex.io/documentation/observable.html)

Since there's "no canonical naming standard" and each implementation has "its own naming quirks", [*we might as well invent our own simplified terminology*][standards] 🤷‍♀️.

### 🏎️ Performance

Sync is pretty performant for what it does. Sync's AutoValue has been benchmarked in comparison to R3's reactive property. You can [see the benchmark source code here](./Chickensoft.Sync.Benchmarks//src/benchmarks/SimpleRepeatedInvoke.cs).

This is a bit of an apples-to-oranges comparison: Sync primitives like AutoValue protect against reentry and allows reactive subjects to define atomic operations, R3 simply invokes functions immediately every time a value changes. Naturally, R3 is about 8-9 times faster since it has essentially no overhead. Both are very fast and do not allocate memory during the hot path (the results are in nanoseconds — billionths of a second). Both scale linearly with the number of invocations, as you'd expect.

Here's the results on an M1 Max laptop:

| Method           | N    |         Mean |        Error |    StdDev | Alloc |
|------------------|------|-------------:|-------------:|----------:|------:|
| ReactiveProperty | 10   |     29.13 ns |     1.002 ns |  0.055 ns |     - |
| AutoValueSet     | 10   |    255.42 ns |     9.659 ns |  0.529 ns |     - |
|                  |      |              |              |           |       |
| ReactiveProperty | 100  |    298.18 ns |    20.567 ns |  1.127 ns |     - |
| AutoValueSet     | 100  |  2,526.14 ns |   316.602 ns | 17.354 ns |     - |
|                  |      |              |              |           |       |
| ReactiveProperty | 1000 |  2,933.28 ns |   337.410 ns | 18.495 ns |     - |
| AutoValueSet     | 1000 | 24,816.69 ns | 1,512.528 ns | 82.907 ns |     - |

Dividing by N to get the average per property set update:

| Method           |     Mean |
|------------------|---------:|
| ReactiveProperty |  2.94 ns |
| AutoValueSet     | 25.21 ns |

With 1,000,000,000 nanoseconds in a second, that's about **340 million updates per second for R3's `ReactiveProperty`** and **40 million updates per second for Chickensoft.Sync's `AutoValue`**.

Or, for 16 ms frame time in a 60 FPS game, that's about 5.7 million sets per frame for R3 and 666,666 per frame for AutoValue. If you need absolute performance and no guarantees, use R3. If you need deterministic single-threaded execution, use Sync. Both are very fast and do not allocate. For UI work, which typically has latency in terms of microseconds, the choice will not matter at all.

### ✅ Correct By Default

When subscribing to changes in a reactive object, your callbacks will observe each change that the object goes through. If you try to mutate the reactive object from those callbacks, you typically want those changes to be deferred until all the callbacks for the current state of the object have finished execution.

By deferring changes, every callback is executed deterministically and in order for each state that the reactive object passes through. Deferral should still happen synchronously via a loop at the outermost stack level, but reactive programming libraries do not do this by default.

For example: the .NET Reactive Extensions (Rx.NET) do not protect against reentrancy by default unless you manually *serialize* a reactive subject (not to be confused with the other "serialization" for saving and loading). Other libraries for C#, such as the aforementioned [R3] reactive programming library, [do not protect against reentrancy at all](./Chickensoft.Sync.Tests/src/R3Comparison.cs), favoring absolute performance instead. Like all systems, you must evaluate the tradeoffs for your particular use case.

> [!NOTE]
> Unless you are building absolutely massive systems, picking correctness and ergonomics over absolute performance will most likely increase the chance of success, since it makes refactoring simpler and safer.

---

🐣 Package generated from a 🐤 Chickensoft Template — <https://chickensoft.games>

[chickensoft-badge]: https://chickensoft.games/img/badges/chickensoft_badge.svg
[chickensoft-website]: https://chickensoft.games
[discord-badge]: https://chickensoft.games/img/badges/discord_badge.svg
[discord]: https://discord.gg/gSjaPgMmYW
[read-the-docs-badge]: https://chickensoft.games/img/badges/read_the_docs_badge.svg
[docs]: https://chickensoft.games/docs
[line-coverage]: Chickensoft.Sync.Tests/badges/line_coverage.svg
[branch-coverage]: Chickensoft.Sync.Tests/badges/branch_coverage.svg
[R3]: https://github.com/Cysharp/R3
[nuget]: https://www.nuget.org/packages/Chickensoft.Sync
[reentrancy]: https://en.wikipedia.org/wiki/Reentrancy_(computing)
[boxing]: https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/types/boxing-and-unboxing
[rx-confusing]: https://www.reddit.com/r/dotnet/comments/1ea7lu6/pros_and_cons_of_using_reactive_extensions_rx_in/
[standards]: https://xkcd.com/927/
[LogicBlocks]: https://github.com/chickensoft-games/LogicBlocks
