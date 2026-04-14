namespace Chickensoft.Sync.Tests.Disposables;

using System;
using System.Collections;
using System.Collections.Generic;
using Chickensoft.Sync.Disposables;
using Moq;
using Shouldly;
using Xunit;

public sealed class CompositeDisposableTest
{
  private static Mock<IDisposable> MockDisposable() => new(MockBehavior.Strict);

  // --- Count ---

  [Fact]
  public void CountReflectsNumberOfDisposablesAdded()
  {
    var composite = new CompositeDisposable();
    composite.Count.ShouldBe(0);

    composite.Add(MockDisposable().Object);
    composite.Count.ShouldBe(1);

    composite.Add(MockDisposable().Object);
    composite.Count.ShouldBe(2);
  }

  // --- Add ---

  [Fact]
  public void AddReturnsTrueWhenDisposableIsNew()
  {
    var composite = new CompositeDisposable();
    var disposable = MockDisposable();

    composite.Add(disposable.Object).ShouldBeTrue();
  }

  [Fact]
  public void AddReturnsFalseWhenDisposableIsAlreadyPresent()
  {
    var composite = new CompositeDisposable();
    var disposable = MockDisposable();

    composite.Add(disposable.Object);
    composite.Add(disposable.Object).ShouldBeFalse();
  }

  [Fact]
  public void AddDisposesImmediatelyWhenAlreadyDisposed()
  {
    var composite = new CompositeDisposable();
    composite.Dispose();

    var disposable = MockDisposable();
    disposable.Setup(d => d.Dispose());

    composite.Add(disposable.Object).ShouldBeFalse();
    disposable.Verify(d => d.Dispose(), Times.Once);
  }

  [Fact]
  public void ICollectionAddDelegatesToAdd()
  {
    var composite = new CompositeDisposable();
    var collection = (ICollection<IDisposable>)composite;

    collection.Add(MockDisposable().Object);
    collection.Count.ShouldBe(1);
  }

  // --- Contains ---

  [Fact]
  public void ContainsReturnsTrueForAddedDisposable()
  {
    var composite = new CompositeDisposable();
    var disposable = MockDisposable();

    composite.Add(disposable.Object);
    composite.Contains(disposable.Object).ShouldBeTrue();
  }

  [Fact]
  public void ContainsReturnsFalseForAbsentDisposable()
  {
    var composite = new CompositeDisposable();

    composite.Contains(MockDisposable().Object).ShouldBeFalse();
  }

  // --- Remove ---

  [Fact]
  public void RemoveReturnsTrueAndDisposesWhenPresent()
  {
    var composite = new CompositeDisposable();
    var disposable = MockDisposable();
    disposable.Setup(d => d.Dispose());

    composite.Add(disposable.Object);
    composite.Remove(disposable.Object).ShouldBeTrue();

    composite.Contains(disposable.Object).ShouldBeFalse();
    disposable.Verify(d => d.Dispose(), Times.Once);
  }

  [Fact]
  public void RemoveReturnsFalseWhenAbsent()
  {
    var composite = new CompositeDisposable();
    var disposable = MockDisposable();

    composite.Remove(disposable.Object).ShouldBeFalse();
  }

  [Fact]
  public void RemoveReturnsFalseWhenCompositeIsDisposed()
  {
    var composite = new CompositeDisposable();
    var disposable = MockDisposable();
    disposable.Setup(d => d.Dispose());

    composite.Add(disposable.Object);
    composite.Dispose();
    disposable.Verify(d => d.Dispose(), Times.Once);

    composite.Remove(disposable.Object).ShouldBeFalse();
  }

  // --- Clear ---

  [Fact]
  public void ClearDisposesAllAndEmptiesCollection()
  {
    var composite = new CompositeDisposable();
    var d1 = MockDisposable();
    var d2 = MockDisposable();
    d1.Setup(d => d.Dispose());
    d2.Setup(d => d.Dispose());

    composite.Add(d1.Object);
    composite.Add(d2.Object);
    composite.Clear();

    composite.Count.ShouldBe(0);
    d1.Verify(d => d.Dispose(), Times.Once);
    d2.Verify(d => d.Dispose(), Times.Once);
  }

  [Fact]
  public void ClearDoesNotDisposeCompositeItself()
  {
    var composite = new CompositeDisposable();
    var disposable = MockDisposable();
    disposable.Setup(d => d.Dispose());

    composite.Add(disposable.Object);
    composite.Clear();

    // After clearing, new disposables can still be added
    composite.Add(MockDisposable().Object).ShouldBeTrue();
  }

  // --- CopyTo ---

  [Fact]
  public void CopyToCopiesDisposablesToArray()
  {
    var composite = new CompositeDisposable();
    var d1 = MockDisposable();
    var d2 = MockDisposable();

    composite.Add(d1.Object);
    composite.Add(d2.Object);

    var array = new IDisposable[2];
    composite.CopyTo(array, 0);

    array.ShouldContain(d1.Object);
    array.ShouldContain(d2.Object);
  }

  [Fact]
  public void CopyToRespectsArrayIndex()
  {
    var composite = new CompositeDisposable();
    var disposable = MockDisposable();

    composite.Add(disposable.Object);

    var array = new IDisposable[3];
    composite.CopyTo(array, 2);

    array[2].ShouldBe(disposable.Object);
  }

  // --- GetEnumerator ---

  [Fact]
  public void GetEnumeratorIteratesOverAllDisposables()
  {
    var composite = new CompositeDisposable();
    var d1 = MockDisposable();
    var d2 = MockDisposable();

    composite.Add(d1.Object);
    composite.Add(d2.Object);

    var seen = new List<IDisposable>();
    foreach (var item in composite)
    {
      seen.Add(item);
    }

    seen.ShouldContain(d1.Object);
    seen.ShouldContain(d2.Object);
    seen.Count.ShouldBe(2);
  }

  [Fact]
  public void NonGenericGetEnumeratorIteratesOverAllDisposables()
  {
    var composite = new CompositeDisposable();
    var disposable = MockDisposable();
    composite.Add(disposable.Object);

    var enumerator = ((IEnumerable)composite).GetEnumerator();
    enumerator.MoveNext().ShouldBeTrue();
    enumerator.Current.ShouldBe(disposable.Object);
  }

  // --- Dispose ---

  [Fact]
  public void DisposeDisposesAllContainedDisposables()
  {
    var composite = new CompositeDisposable();
    var d1 = MockDisposable();
    var d2 = MockDisposable();
    d1.Setup(d => d.Dispose());
    d2.Setup(d => d.Dispose());

    composite.Add(d1.Object);
    composite.Add(d2.Object);
    composite.Dispose();

    d1.Verify(d => d.Dispose(), Times.Once);
    d2.Verify(d => d.Dispose(), Times.Once);
  }

  [Fact]
  public void DisposeIsIdempotent()
  {
    var composite = new CompositeDisposable();
    var disposable = MockDisposable();
    disposable.Setup(d => d.Dispose());

    composite.Add(disposable.Object);
    composite.Dispose();
    composite.Dispose(); // should not throw or double-dispose

    disposable.Verify(d => d.Dispose(), Times.Once);
  }

  [Fact]
  public void IsReadOnlyReturnsFalse()
  {
    var composite = new CompositeDisposable();
    ((ICollection<IDisposable>)composite).IsReadOnly.ShouldBeFalse();
  }
}
