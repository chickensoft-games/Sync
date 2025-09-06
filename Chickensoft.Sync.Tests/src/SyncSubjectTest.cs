namespace Chickensoft.Sync.Tests;

using System;
using System.Collections.Generic;
using Moq;
using Shouldly;
using Xunit;

public sealed class SyncSubjectTest {
  public sealed class TestOwner<T> : IPerform<T> where T : struct {
    public SyncSubject Subject { get; set; } = default!;

    public required Action<SyncSubject, T> Action { get; init; }

    public void Perform(in T op) { Action(Subject, op); }
  }

  public TestOwner<int> Nop => new TestOwner<int>() {
    Action = (SyncSubject _, int __) => { }
  };

  public SyncSubject BuildSubject() {
    var owner = new TestOwner<int> {
      Action = (SyncSubject subj, int op) => subj.Broadcast(op)
    };
    var subject = new SyncSubject(owner);
    owner.Subject = subject;
    return subject;
  }

  [Fact]
  public void InitializesAndDisposes() {
    using var subject = new SyncSubject(Nop);
  }

  [Fact]
  public void AddBindingThrowsIfDisposed() {
    var subject = new SyncSubject(Nop);
    subject.Dispose();

    Should.Throw<ObjectDisposedException>(() =>
      subject.AddBinding(new Mock<ISyncBinding>().Object)
    );
  }

  [Fact]
  public void RemoveBindingThrowsIfDisposed() {
    var subject = new SyncSubject(Nop);
    subject.Dispose();

    Should.Throw<ObjectDisposedException>(() =>
      subject.RemoveBinding(new Mock<ISyncBinding>().Object)
    );
  }

  [Fact]
  public void ClearBindingsThrowsIfDisposed() {
    var subject = new SyncSubject(Nop);
    subject.Dispose();

    Should.Throw<ObjectDisposedException>(subject.ClearBindings);
  }

  [Fact]
  public void PerformThrowsIfDisposed() {
    var subject = new SyncSubject(Nop);
    subject.Dispose();

    Should.Throw<ObjectDisposedException>(() => subject.Perform(1));
  }

  [Fact]
  public void BroadcastThrowsIfDisposed() {
    var subject = new SyncSubject(Nop);
    subject.Dispose();

    Should.Throw<ObjectDisposedException>(() => subject.Broadcast(1));
  }

  // serialized in the ReactiveX sense of
  // "protects against re-entry by deferring"

  [Fact]
  public void AddsBindingSerialized() {
    var subject = BuildSubject();

    var binding1 = new Mock<ISyncBinding>();
    var binding2 = new Mock<ISyncBinding>();

    var calls = 0;

    binding1.Setup(b => b.InvokeCallbacks(It.Ref<int>.IsAny))
      .Callback((in int value) => {
        calls++;

        if (calls == 1) {
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
  public void RemovesBindingSerialized() {
    var subject = BuildSubject();

    var binding1 = new Mock<ISyncBinding>();
    var binding2 = new Mock<ISyncBinding>();

    var calls = 0;

    binding1.Setup(b => b.InvokeCallbacks(It.Ref<int>.IsAny))
      .Callback((in int value) => {
        calls++;

        if (calls == 1) {
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
  public void ClearsBindingsSerialized() {
    var subject = BuildSubject();

    var binding1 = new Mock<ISyncBinding>();
    var binding2 = new Mock<ISyncBinding>();

    var calls = 0;

    binding1.Setup(b => b.InvokeCallbacks(It.Ref<int>.IsAny))
      .Callback((in int value) => {
        calls++;

        if (calls == 1) {
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
  public void PerformsOpsSerialized() {
    var log = new List<string>();
    var owner = new TestOwner<int>() {
      Action = (SyncSubject subj, int value) => {
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
      .Callback((in int value) => {
        log.Add($"callback {value}");
        calls++;

        subject.IsBusy.ShouldBeTrue();

        if (calls == 1) {
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
  public void DisposesSerialized() {
    var subject = BuildSubject();

    var binding1 = new Mock<ISyncBinding>();

    var calls = 0;

    binding1.Setup(b => b.InvokeCallbacks(It.Ref<int>.IsAny))
      .Callback((in int value) => {
        calls++;

        if (calls == 1) {
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
}
