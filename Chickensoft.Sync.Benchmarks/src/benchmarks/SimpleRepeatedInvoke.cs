namespace Chickensoft.Sync.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Order;
using Chickensoft.Sync.Primitives;
using R3;

// Silly benchmark to make sure we can detect memory allocations. :P
[ShortRunJob]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class Allocation {
  [Benchmark(Baseline = true)]
  public object MakeObject() {
    return new object();
  }

  [Benchmark]
  public (object, object) MakeTwoObjects() => (new object(), new object());
}

[ShortRunJob]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class SimpleRepeatedInvoke {
  private ReactiveProperty<int> _r3ReactiveProperty = default!;
  private IDisposable _r3Listener = default!;

  private AutoValue<int> _autoValue = default!;
  private AutoValue<int>.Binding _autoValueBinding = default!;

  // How many increasing values to push per benchmark invocation.
  [Params(10, 100, 1000)]
  public int N;

  [GlobalSetup]
  public void Setup() {
    _r3ReactiveProperty = new ReactiveProperty<int>(0);
    _r3Listener = _r3ReactiveProperty.Subscribe(static _ => { });

    _autoValue = new AutoValue<int>(0);
    _autoValueBinding = _autoValue.Bind().OnValue(static _ => { });
  }

  [GlobalCleanup]
  public void Cleanup() {
    _r3Listener.Dispose();
    _autoValueBinding.Dispose();
  }

  [Benchmark(Baseline = true)]
  public void R3ReactiveProperty() {
    for (var i = 0; i < N; i++) {
      _r3ReactiveProperty.Value = i;
    }
  }

  [Benchmark]
  public void AutoValueSet() {
    for (var i = 0; i < N; i++) {
      _autoValue.Value = i;
    }
  }
}
