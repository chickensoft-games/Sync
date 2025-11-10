namespace Chickensoft.Sync.Tests;

using System;
using System.Collections.Generic;
using Moq;
using Shouldly;
using Xunit;

public sealed class SyncSubjectTest
{
  public sealed class TestOwner<T> : IPerform<T> where T : struct
  {
    public SyncSubject Subject { get; set; } = default!;

    public required Action<SyncSubject, T> Action { get; init; }

    public void Perform(in T op) => Action(Subject, op);
  }

  public sealed class TestOwnerAny :
    IPerformAnyOperation,
    IPerform<TestOwnerAny.TestOp1>,
    IPerform<TestOwnerAny.TestOp2>
  {
    public readonly record struct TestOp1;
    public readonly record struct TestOp2;
    public SyncSubject Subject { get; set; } = default!;

    public required Action<SyncSubject, object> Action { get; set; }
    public required Action<SyncSubject, TestOp1> TestAction1 { get; init; }
    public required Action<SyncSubject, TestOp2> TestAction2 { get; init; }

    public void Perform<TOp>(in TOp op) where TOp : struct => Action(Subject, op);
    public void Perform(in TestOp1 op) => TestAction1(Subject, op);
    public void Perform(in TestOp2 op) => TestAction2(Subject, op);
  }

  public TestOwner<int> Nop => new()
  {
    Action = (_, __) => { }
  };

  public SyncSubject BuildSubject()
  {
    var owner = new TestOwner<int>
    {
      Action = (subj, op) => subj.Broadcast(op)
    };
    var subject = new SyncSubject(owner);
    owner.Subject = subject;
    return subject;
  }

  [Fact]
  public void InitializesAndDisposes()
  {
    using var subject = new SyncSubject(Nop);
  }

  [Fact]
  public void AddBindingThrowsIfDisposed()
  {
    var subject = new SyncSubject(Nop);
    subject.Dispose();

    Should.Throw<ObjectDisposedException>(() =>
      subject.AddBinding(new Mock<ISyncBinding>().Object)
    );
  }

  [Fact]
  public void RemoveBindingThrowsIfDisposed()
  {
    var subject = new SyncSubject(Nop);
    subject.Dispose();

    Should.Throw<ObjectDisposedException>(() =>
      subject.RemoveBinding(new Mock<ISyncBinding>().Object)
    );
  }

  [Fact]
  public void ClearBindingsThrowsIfDisposed()
  {
    var subject = new SyncSubject(Nop);
    subject.Dispose();

    Should.Throw<ObjectDisposedException>(subject.ClearBindings);
  }

  [Fact]
  public void PerformThrowsIfDisposed()
  {
    var subject = new SyncSubject(Nop);
    subject.Dispose();

    Should.Throw<ObjectDisposedException>(() => subject.Perform(1));
  }

  [Fact]
  public void BroadcastThrowsIfDisposed()
  {
    var subject = new SyncSubject(Nop);
    subject.Dispose();

    Should.Throw<ObjectDisposedException>(() => subject.Broadcast(1));
  }

  // serialized in the ReactiveX sense of
  // "protects against re-entry by deferring"

  [Fact]
  public void AddsBindingSerialized()
  {
    var subject = BuildSubject();

    var binding1 = new Mock<ISyncBinding>();
    var binding2 = new Mock<ISyncBinding>();

    var calls = 0;

    binding1.Setup(b => b.InvokeCallbacks(It.Ref<int>.IsAny))
      .Callback((in int value) =>
      {
        calls++;

        if (calls == 1)
        {
          subject.AddBinding(binding2.Object);
          // this should not be available yet
          subject._bindings.ShouldNotContain(binding2.Object);
        }
      });

    subject.AddBinding(binding1.Object);
    subject.Perform(1);

    subject._bindings.ShouldContain(binding2.Object);
  }

  [Fact]
  public void RemovesBindingSerialized()
  {
    var subject = BuildSubject();

    var binding1 = new Mock<ISyncBinding>();
    var binding2 = new Mock<ISyncBinding>();

    var calls = 0;

    binding1.Setup(b => b.InvokeCallbacks(It.Ref<int>.IsAny))
      .Callback((in int value) =>
      {
        calls++;

        if (calls == 1)
        {
          subject.RemoveBinding(binding2.Object);
          // this should not be removed yet
          subject._bindings.ShouldContain(binding2.Object);
        }
      });

    subject.AddBinding(binding1.Object);
    subject.AddBinding(binding2.Object);
    subject.Perform(1);

    subject._bindings.ShouldNotContain(binding2.Object);
  }

  [Fact]
  public void ClearsBindingsSerialized()
  {
    var subject = BuildSubject();

    var binding1 = new Mock<ISyncBinding>();
    var binding2 = new Mock<ISyncBinding>();

    var calls = 0;

    binding1.Setup(b => b.InvokeCallbacks(It.Ref<int>.IsAny))
      .Callback((in int value) =>
      {
        calls++;

        if (calls == 1)
        {
          subject.ClearBindings();
          // these should not be cleared yet
          subject._bindings.ShouldContain(binding1.Object);
          subject._bindings.ShouldContain(binding2.Object);
        }
      });

    subject.AddBinding(binding1.Object);
    subject.AddBinding(binding2.Object);
    subject.Perform(1);

    subject._bindings.ShouldBeEmpty();
  }

  [Fact]
  public void PerformsOpsSerialized()
  {
    var log = new List<string>();
    var owner = new TestOwner<int>()
    {
      Action = (subj, value) =>
      {
        log.Add($"owner {value}");
        subj.Broadcast(value);
      }
    };

    var subject = new SyncSubject(owner);
    owner.Subject = subject;

    var binding1 = new Mock<ISyncBinding>();
    var binding2 = new Mock<ISyncBinding>();

    var calls = 0;

    binding1.Setup(b => b.InvokeCallbacks(It.Ref<int>.IsAny))
      .Callback((in int value) =>
      {
        log.Add($"callback {value}");
        calls++;

        subject.IsBusy.ShouldBeTrue();

        if (calls == 1)
        {
          subject.Perform(2);
          // this should not be broadcast yet
          binding2.Verify(
            b2 => b2.InvokeCallbacks(It.Ref<int>.IsAny), Times.Never
          );
        }
      });

    subject.AddBinding(binding1.Object);
    subject.AddBinding(binding2.Object);
    subject.Perform(1);
    subject.IsBusy.ShouldBeFalse();

    binding2.Verify(b2 => b2.InvokeCallbacks(1));
    binding2.Verify(b2 => b2.InvokeCallbacks(2));

    // owner should run before bindings each time
    log.ShouldBe(["owner 1", "callback 1", "owner 2", "callback 2"]);
  }

  [Fact]
  public void PerformsAnyOpsSerialized()
  {
    var callback = "callback";
    var anyAction = nameof(TestOwnerAny.Action);
    var testAction1 = nameof(TestOwnerAny.TestAction1);
    var testAction2 = nameof(TestOwnerAny.TestAction2);

    var testOp1 = new TestOwnerAny.TestOp1();
    var testOp2 = new TestOwnerAny.TestOp2();
    var number2 = 2;

    var log = new List<string>();

    var owner = new TestOwnerAny
    {
      Action = (subj, value) =>
      {
        log.Add($"{anyAction} {value}");
        if (value is int number)
          subj.Broadcast(number);
      },
      TestAction1 = (subj, value) =>
      {
        log.Add($"{testAction1} {value}");
        subj.Broadcast(value);
      },
      TestAction2 = (subj, value) =>
      {
        log.Add($"{testAction2} {value}");
        subj.Broadcast(value);
      }
    };

    var subject = new SyncSubject(owner);
    owner.Subject = subject;

    var binding1 = new Mock<ISyncBinding>();
    var binding2 = new Mock<ISyncBinding>();

    var calls = 0;

    binding1.Setup(b => b.InvokeCallbacks(It.Ref<TestOwnerAny.TestOp1>.IsAny))
      .Callback((in TestOwnerAny.TestOp1 value) =>
      {
        log.Add($"{callback} {value}");
        calls++;

        subject.IsBusy.ShouldBeTrue();

        if (calls == 1)
        {
          subject.Perform(testOp2);
          // this should not be broadcast yet
          binding2.Verify(
            b2 => b2.InvokeCallbacks(It.Ref<TestOwnerAny.TestOp2>.IsAny), Times.Never
          );
        }
      });

    subject.AddBinding(binding1.Object);
    subject.AddBinding(binding2.Object);
    subject.Perform(testOp1);
    subject.Perform(number2);
    subject.IsBusy.ShouldBeFalse();

    binding2.Verify(b2 => b2.InvokeCallbacks(It.Ref<TestOwnerAny.TestOp1>.IsAny));
    binding2.Verify(b2 => b2.InvokeCallbacks(It.Ref<TestOwnerAny.TestOp2>.IsAny));
    binding2.Verify(b2 => b2.InvokeCallbacks(2));

    // Order should be IPerform then IPerformAnyOperation
    log.ShouldBe([
      $"{testAction1} {testOp1}", // IPerform<TestOp1>
      $"{callback} {testOp1}", // callback for IPerform<TestOp1>
      $"{anyAction} {testOp1}", // IPerformAnyOperation (TestOp1)
      $"{testAction2} {testOp2}", //IPerform<TestOp2>
      $"{anyAction} {testOp2}", //IPerformAnyOperation (TestOp2)
      $"{anyAction} {number2}" //IPerformAnyOperation (int)
    ]);
  }

  [Fact]
  public void DisposesSerialized()
  {
    var subject = BuildSubject();

    var binding1 = new Mock<ISyncBinding>();

    var calls = 0;

    binding1.Setup(b => b.InvokeCallbacks(It.Ref<int>.IsAny))
      .Callback((in int value) =>
      {
        calls++;

        if (calls == 1)
        {
          subject.Dispose();
          // should not be disposed yet
          subject._isDisposed.ShouldBeFalse();
        }
      });

    subject.AddBinding(binding1.Object);
    subject.Perform(1);

    subject._isDisposed.ShouldBeTrue();

    // subsequent dispose should do nothing :)
    Should.NotThrow(subject.Dispose);
  }

  [Fact]
  public void ClearThrowsDisposed()
  {
    var subject = new SyncSubject(Nop);
    subject.Dispose();

    Should.Throw<ObjectDisposedException>(subject.Clear);
  }

  [Fact]
  public void ClearsPendingOperations()
  {
    var subject = BuildSubject();

    var binding1 = new Mock<ISyncBinding>();

    var calls = 0;

    binding1.Setup(b => b.InvokeCallbacks(It.Ref<int>.IsAny))
      .Callback((in int value) =>
      {
        calls++;

        if (calls == 1)
        {
          subject.Perform(2); // never happens because we clear
          subject.Clear();
        }
      });

    subject.AddBinding(binding1.Object);

    subject.Perform(1);

    calls.ShouldBe(1);
  }
}
