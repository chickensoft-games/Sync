namespace Chickensoft.Sync.Tests.Disposables;

using System;
using Chickensoft.Sync.Disposables;
using Moq;
using Shouldly;

public sealed class DisposableExtensionsTest
{
  [Fact]
  public void DisposeWithAddsDisposableToComposite()
  {
    var composite = new CompositeDisposable();
    var disposable = new Mock<IDisposable>(MockBehavior.Strict);
    disposable.Object.DisposeWith(composite);

    composite.Contains(disposable.Object).ShouldBeTrue();
  }

  [Fact]
  public void DisposeWithReturnsTheDisposable()
  {
    var composite = new CompositeDisposable();
    var disposable = new Mock<IDisposable>(MockBehavior.Strict);

    var returned = disposable.Object.DisposeWith(composite);

    returned.ShouldBe(disposable.Object);
  }
}

