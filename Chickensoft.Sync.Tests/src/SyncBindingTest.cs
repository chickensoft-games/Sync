namespace Chickensoft.Sync.Tests;

using System;
using System.Collections.Generic;
using Moq;
using Shouldly;
using Xunit;

public sealed class SyncBindingTest {
  private sealed class TestBinding : SyncBinding {
    public TestBinding(ISyncSubject subject) : base(subject) { }
  }

  [Fact]
  public void InitializesAndDisposes() {
    var subject = new Mock<ISyncSubject>();
    var binding = new TestBinding(subject.Object);

    subject.Verify(s => s.AddBinding(binding));
    binding._subject.ShouldBe(subject.Object);

    binding.Dispose();

    subject.Verify(s => s.RemoveBinding(binding));
    binding._subject.ShouldBeNull();

    Should.NotThrow(binding.Dispose);
  }

  [Fact]
  public void InitializationFailsIfSubjectDisposed() {
    var subject = new Mock<ISyncSubject>();
    subject.Setup(s => s.IsDisposed).Returns(true);

    Should.Throw<ObjectDisposedException>(() =>
      new TestBinding(subject.Object)
    );
  }

  [Fact]
  public void AddCallbackThrowsIfDisposed() {
    var subject = new Mock<ISyncSubject>();
    var binding = new TestBinding(subject.Object);
    binding.Dispose();

    Should.Throw<ObjectDisposedException>(() =>
      binding.AddCallback((in int _) => { })
    );
  }

  [Fact]
  public void AddsMultipleCallbacksAndInvokesThemInOrder() {
    var subject = new Mock<ISyncSubject>();
    var binding = new TestBinding(subject.Object);

    var calls = new List<int>();

    binding.AddCallback((in int b) => calls.Add(b));
    binding.AddCallback((in int b) => calls.Add(b * 2));
    binding.AddCallback((in int b) => calls.Add(b * 3));

    binding.InvokeCallbacks(2);

    calls.Count.ShouldBe(3);
    calls[0].ShouldBe(2);
    calls[1].ShouldBe(4);
    calls[2].ShouldBe(6);
  }

  [Fact]
  public void InvokeCallbacksThrowsIfDisposed() {
    var subject = new Mock<ISyncSubject>();
    var binding = new TestBinding(subject.Object);
    binding.Dispose();

    Should.Throw<ObjectDisposedException>(() =>
      binding.InvokeCallbacks(1)
    );
  }
}
