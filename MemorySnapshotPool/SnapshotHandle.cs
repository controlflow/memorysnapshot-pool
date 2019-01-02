using System;

namespace MemorySnapshotPool
{
  public readonly struct SnapshotHandle : IEquatable<SnapshotHandle>
  {
    public readonly uint Handle;

    public SnapshotHandle(uint handle)
    {
      Handle = handle;
    }

    public static bool operator ==(SnapshotHandle left, SnapshotHandle right) => left.Equals(right);
    public static bool operator !=(SnapshotHandle left, SnapshotHandle right) => !left.Equals(right);

    public bool Equals(SnapshotHandle other) => Handle == other.Handle;

    public override bool Equals(object obj) => throw new InvalidOperationException();

    public override int GetHashCode() => (int) Handle;

    public override string ToString() => Handle.ToString();
  }
}