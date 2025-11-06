namespace Chickensoft.Sync.Tests;

using R3;
using Shouldly;

public sealed class R3Comparison
{
  // This test fails because R3's ReactiveProperty immediately re-enters handlers
  // when a value is mutated inside a handler.
  // Chickensoft.Sync does not do this. However, R3 outperforms it, likely because
  // it doesn't have to do the extra work to prevent re-entrancy.

#pragma warning disable xUnit1004 // Test methods should not be skipped
  [Fact(Skip = "ReactiveProperty in R3 does not protect against reentrancy.")]
  public void ReactivePropertyProtectsAgainstReEntrancy()
  {
    var rp = new ReactiveProperty<int>(0);
    var events = new List<int>();

    var inCallback = false;
    var reentered = false;

    rp.Subscribe(x =>
    {
      if (inCallback)
      {
        reentered = true; // signals immediate (re-entrant) delivery
      }

      inCallback = true;

      events.Add(x);

      if (x == 1)
      {
        rp.Value = 2; // attempt re-entrant set
      }

      inCallback = false;
    });

    // Triggers: initial 0 on subscribe, then 1; inside handler we set 2.
    rp.Value = 1;

    // expected to pass
    rp.Value.ShouldBe(2);
    events.ShouldBe([0, 1, 2]);

    // if r3 re-enters immediately when mutating a value inside a handler,
    // this will fail here.
    reentered.ShouldBeFalse();
  }
#pragma warning restore xUnit1004
}
