namespace Chickensoft.Sync;

using System;

public sealed class DisposeException : Exception
{
  public DisposeException() { }
  public DisposeException(string message) : base(message) { }
  public DisposeException(string message, Exception innerException) : base(message, innerException) { }
}
