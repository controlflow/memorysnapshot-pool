using System;
using System.Diagnostics.CodeAnalysis;

namespace MemorySnapshotPool
{
  public readonly struct SnapshotHandle : IEquatable<SnapshotHandle>
  {
    public readonly uint Handle;

    public SnapshotHandle(uint handle)
    {
      Handle = handle;
    }

    private const uint SizeMask = 0xFF00_0000;
    private const int SizeOffset = 24;

    // only for variable-size snapshots
    internal uint SnapshotSizeInBytes => (Handle & SizeMask) >> SizeOffset;
    internal uint SnapshotHandleWithoutSize => Handle & ~SizeMask;

    public static bool operator ==(SnapshotHandle left, SnapshotHandle right) => left.Equals(right);
    public static bool operator !=(SnapshotHandle left, SnapshotHandle right) => !left.Equals(right);

    public bool Equals(SnapshotHandle other) => Handle == other.Handle;

    public override bool Equals(object obj) => throw new InvalidOperationException();

    public override int GetHashCode() => (int) Handle;

    public override string ToString() => Handle.ToString();

    public static readonly SnapshotHandle Zero = new SnapshotHandle(0);
  }
}