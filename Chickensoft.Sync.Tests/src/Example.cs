namespace Chickensoft.Sync.Tests;

using Chickensoft.Sync.Primitives;

public class AutoValueSimpleExample {
  public void Example() {
    // hang onto the value for as long as you want to change it, then call Dispose()
    // when you're done with it
    var autoValue = new AutoValue<Animal>(new Cat("Pickles"));

    // hang onto the binding for as long as you want to observe, then call Dispose()
    // on it
    using var binding = autoValue.Bind();

    binding
      // called whenever the value changes
      .OnValue(animal => Console.WriteLine($"Observing animal {animal}"))
      // only called for Dog values
      .OnValue((Dog dog) => Console.WriteLine($"Observing dog {dog.Name}"))
      // only called for Cat values
      .OnValue((Cat cat) => Console.WriteLine($"Observing cat {cat.Name}"))
      // only called for Dog values beginning with 'B'
      .OnValue(
        (Dog dog) => Console.WriteLine($"Observing dog with B name {dog.Name}"),
        condition: dog => dog.Name.StartsWith('B')
      );

    autoValue.Value = new Dog("Brisket");
    autoValue.Value = new Cat("Chibi");
  }
}

// Enemy gameplay logic
public sealed class Enemy : IDisposable {
  // mutable observable value private to this value
  private readonly AutoValue<int> _health = new(100);

  // immutable view of the value for outside subscribers
  public IAutoValue<int> Health => _health;

  public void TakeDamage(int damage) {
    // enemy can't take more damage than it has health
    var appliedDamage = Math.Min(Math.Abs(damage), _health.Value);
    // bindings will be notified when this goes into effect
    _health.Value -= appliedDamage;
  }

  public void Dispose() {
    // release references to any bindings to the health value so they can be GC'd
    _health.Dispose();
  }
}

// Enemy visualization logic
public sealed class EnemyView : IDisposable {
  public Enemy Enemy { get; }
  public AutoValue<int>.Binding Binding { get; }

  public EnemyView(Enemy enemy) {
    Enemy = enemy;
    // listen to changes in the enemy's health
    Binding = enemy.Health.Bind();
    Binding.OnValue(OnHealthChanged);
  }

  public void OnHealthChanged(int health) {
    // update the health bar UI, etc.
  }

  public void Dispose() {
    Binding.Dispose(); // stop listening
  }
}

public class AutoListExample() {
  public void Example() {
    var autoList = new AutoList<Animal>([
      new Cat("Pickles"),
      new Dog("Cookie"),
      new Dog("Brisket"),
      new Cat("Sven")
    ]);

    // hang onto the binding for as long as you want to observe, then call Dispose()
    // on it
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
  }
}

public class AutoSetExample {
  public void Example() {
    var autoSet = new AutoSet<Animal>(new HashSet<Animal> {
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
  }
}

public class AutoMapExample {
  public void Example() {
    var autoMap = new AutoMap<string, Animal>(new Dictionary<string, Animal> {
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
    autoMap["Brisket"] = new Cat("Brisket");
  }
}
