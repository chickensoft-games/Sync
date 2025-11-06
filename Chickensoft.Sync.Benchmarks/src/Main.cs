namespace Chickensoft.Sync.Benchmarks;

using BenchmarkDotNet.Running;

public static class Program
{
  public static void Main(string[] args) =>
    BenchmarkRunner.Run(typeof(Program).Assembly);
}
