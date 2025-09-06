namespace Chickensoft.Sync.Benchmarks;

using BenchmarkDotNet.Running;

public static class Program {
  public static void Main(string[] args) {
    var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
  }
}
