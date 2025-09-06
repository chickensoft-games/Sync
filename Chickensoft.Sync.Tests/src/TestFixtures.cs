namespace Chickensoft.Sync.Tests;

public abstract record Animal(string Name);
public record Dog(string Name) : Animal(Name);
public sealed record Poodle(string Name) : Dog(Name);
public sealed record Cat(string Name) : Animal(Name);
