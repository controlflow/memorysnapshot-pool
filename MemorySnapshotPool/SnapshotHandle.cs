using System;

namespace MemorySnapshotPool
{
  public struct SnapshotHandle : IEquatable<SnapshotHandle>
  {
    public readonly uint Handle;

    public SnapshotHandle(uint handle)
    {
      Handle = handle;
    }

    public static bool operator ==(SnapshotHandle left, SnapshotHandle right)
    {
      return left.Equals(right);
    }

    public static bool operator !=(SnapshotHandle left, SnapshotHandle right)
    {
      return !left.Equals(right);
    }

    public bool Equals(SnapshotHandle other)
    {
      return Handle == other.Handle;
    }

    public override bool Equals(object obj)
    {
      throw new InvalidOperationException();
    }

    public override int GetHashCode()
    {
      return (int) Handle;
    }

    public override string ToString()
    {
      return Handle.ToString();
    }
  }
}