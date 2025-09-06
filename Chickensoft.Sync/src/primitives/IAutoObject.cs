namespace Chickensoft.Sync.Primitives;

using System;

/// <summary>An observable object.</summary>
public interface IAutoObject<TBinding> : IDisposable where TBinding : SyncBinding {
  /// <summary>
  /// Creates a new binding that listens to changes in the object. The
  /// binding is automatically setup to observe this object.
  /// </summary>
  TBinding Bind();

  /// <summary>
  /// Removes all bindings from this object. If any bindings are currently
  /// processing other operations, this is deferred until after the current
  /// operations are completed.
  /// </summary>
  void ClearBindings();
}
