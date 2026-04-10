namespace Chickensoft.Sync.Tests.Disposables;

using System;
using System.Threading.Tasks;
using Chickensoft.Sync.Disposables;
using Shouldly;
using Xunit;

public sealed class CascadeDisposableTest
{
  private sealed class SyncOnly : IDisposable
  {
    public int DisposeCount { get; private set; }
    public void Dispose() => DisposeCount++;
  }

  private sealed class AsyncOnly : IAsyncDisposable
  {
    public int DisposeAsyncCount { get; private set; }
    public ValueTask DisposeAsync() { DisposeAsyncCount++; return default; }
  }

  private sealed class DualDisposable : IDisposable, IAsyncDisposable
  {
    public int DisposeCount { get; private set; }
    public int DisposeAsyncCount { get; private set; }
    public void Dispose() => DisposeCount++;
    public ValueTask DisposeAsync() { DisposeAsyncCount++; return default; }
  }

  // --- Create ---

  [Fact]
  public void Create_WithSyncOnly_SetsDisposable_AndNullAsync()
  {
    var inner = new SyncOnly();
    var cascade = CascadeDisposable.Create(inner);

    cascade.Disposable.ShouldBeSameAs(inner);
    cascade.AsyncDisposable.ShouldBeNull();
  }

  [Fact]
  public void Create_WithSyncDisposable_CastsToAsync_WhenDual()
  {
    var inner = new DualDisposable();
    var cascade = CascadeDisposable.Create((IDisposable)inner);

    cascade.Disposable.ShouldBeSameAs(inner);
    cascade.AsyncDisposable.ShouldBeSameAs(inner);
  }

  [Fact]
  public void Create_WithAsyncOnly_SetsAsyncDisposable_AndNullSync()
  {
    var inner = new AsyncOnly();
    var cascade = CascadeDisposable.Create(inner);

    cascade.Disposable.ShouldBeNull();
    cascade.AsyncDisposable.ShouldBeSameAs(inner);
  }

  [Fact]
  public void Create_WithAsyncDisposable_CastsToSync_WhenDual()
  {
    var inner = new DualDisposable();
    var cascade = CascadeDisposable.Create((IAsyncDisposable)inner);

    cascade.Disposable.ShouldBeSameAs(inner);
    cascade.AsyncDisposable.ShouldBeSameAs(inner);
  }

  [Fact]
  public void Create_GenericOverload_SetsBoth()
  {
    var inner = new DualDisposable();
    var cascade = CascadeDisposable.Create(inner);

    cascade.Disposable.ShouldBeSameAs(inner);
    cascade.AsyncDisposable.ShouldBeSameAs(inner);
  }

  // --- Dispose ---

  [Fact]
  public void Dispose_CallsDisposeOnUnderlyingDisposable()
  {
    var inner = new SyncOnly();
    var cascade = CascadeDisposable.Create(inner);

    cascade.Dispose();

    inner.DisposeCount.ShouldBe(1);
  }

  [Fact]
  public void Dispose_IsIdempotent()
  {
    var inner = new SyncOnly();
    var cascade = CascadeDisposable.Create(inner);

    cascade.Dispose();
    cascade.Dispose();

    inner.DisposeCount.ShouldBe(1);
  }

  [Fact]
  public void Dispose_IsNoOp_WhenDisposableIsNull()
  {
    // Created via async-only: _isDisposed starts true, so Dispose() skips the body entirely.
    var cascade = CascadeDisposable.Create(new AsyncOnly());

    Should.NotThrow(cascade.Dispose);
  }

  [Fact]
  public void Dispose_DoesNotDisposeAsyncPart()
  {
    var inner = new DualDisposable();
    var cascade = CascadeDisposable.Create(inner);

    cascade.Dispose();

    inner.DisposeAsyncCount.ShouldBe(0);
  }

  // --- DisposeAsync ---

  [Fact]
  public async Task DisposeAsync_CallsDisposeAsyncOnUnderlyingAsyncDisposable()
  {
    var inner = new AsyncOnly();
    var cascade = CascadeDisposable.Create(inner);

    await cascade.DisposeAsync();

    inner.DisposeAsyncCount.ShouldBe(1);
  }

  [Fact]
  public async Task DisposeAsync_AlsoCallsDispose()
  {
    var inner = new DualDisposable();
    var cascade = CascadeDisposable.Create(inner);

    await cascade.DisposeAsync();

    inner.DisposeAsyncCount.ShouldBe(1);
    inner.DisposeCount.ShouldBe(1);
  }

  [Fact]
  public async Task DisposeAsync_SkipsAsyncBranch_WhenAsyncDisposableIsNull()
  {
    // Created via sync-only: _isAsyncDisposed starts true, so the async body is skipped.
    // Dispose() still runs, verifying the always-runs tail of DisposeAsync.
    var inner = new SyncOnly();
    var cascade = CascadeDisposable.Create(inner);

    await cascade.DisposeAsync();

    inner.DisposeCount.ShouldBe(1);
  }

  // --- Equals ---

  [Fact]
  public void Equals_ReturnsTrue_ForSameUnderlyingDisposables()
  {
    var inner = new DualDisposable();
    var a = CascadeDisposable.Create(inner);
    var b = CascadeDisposable.Create(inner);

    a.Equals(b).ShouldBeTrue();
  }

  [Fact]
  public void Equals_ReturnsFalse_WhenDisposableDiffers()
  {
    // Different Disposable references short-circuit the && before checking AsyncDisposable.
    var a = CascadeDisposable.Create(new SyncOnly());
    var b = CascadeDisposable.Create(new SyncOnly());

    a.Equals(b).ShouldBeFalse();
  }

  [Fact]
  public void Equals_ReturnsFalse_WhenAsyncDisposableDiffers()
  {
    // Both have Disposable == null (async-only) so the first operand matches,
    // exercising the second operand of &&.
    var a = CascadeDisposable.Create(new AsyncOnly());
    var b = CascadeDisposable.Create(new AsyncOnly());

    a.Equals(b).ShouldBeFalse();
  }

  [Fact]
  public void Equals_Object_ReturnsTrue_ForMatchingCascadeDisposable()
  {
    var inner = new DualDisposable();
    var a = CascadeDisposable.Create(inner);
    object b = CascadeDisposable.Create(inner);

    a.Equals(b).ShouldBeTrue();
  }

  [Fact]
  public void Equals_Object_ReturnsFalse_ForNonMatchingCascadeDisposable()
  {
    var a = CascadeDisposable.Create(new SyncOnly());
    object b = CascadeDisposable.Create(new SyncOnly());

    a.Equals(b).ShouldBeFalse();
  }

  [Fact]
  public void Equals_Object_ReturnsFalse_ForDifferentType()
  {
    var cascade = CascadeDisposable.Create(new SyncOnly());

    cascade.Equals("not a CascadeDisposable").ShouldBeFalse();
  }

  // --- GetHashCode ---

  [Fact]
  public void GetHashCode_ReturnsSameValue_ForEqualInstances()
  {
    var inner = new DualDisposable();
    var a = CascadeDisposable.Create(inner);
    var b = CascadeDisposable.Create(inner);

    a.GetHashCode().ShouldBe(b.GetHashCode());
  }
}
