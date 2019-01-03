using JetBrains.Annotations;

namespace MemorySnapshotPool.View
{
  public readonly struct SnapshotPoolByteView
  {
    [NotNull] private readonly SnapshotPool mySnapshotPool;

    public SnapshotPoolByteView([NotNull] SnapshotPool snapshotPool)
    {
      mySnapshotPool = snapshotPool;
    }

    [Pure]
    private int ElementShift(uint elementIndex) => (int) ((elementIndex % 4) * 8);

    [Pure]
    public byte GetByte(SnapshotHandle snapshot, uint elementIndex)
    {
      var uintValue = mySnapshotPool.GetUint32(snapshot, elementIndex / 4);
      var shiftedValue = uintValue >> ElementShift(elementIndex);

      return (byte) shiftedValue;
    }

    [MustUseReturnValue]
    public SnapshotHandle SetSingleByte(SnapshotHandle snapshot, uint elementIndex, byte value)
    {
      var uintValue = mySnapshotPool.GetUint32(snapshot, elementIndex / 4);
      var modifiedValue = uintValue | (uint) (value << ElementShift(elementIndex));

      return mySnapshotPool.SetSingleUInt32(snapshot, elementIndex / 4, modifiedValue);
    }

    [MustUseReturnValue]
    public SnapshotHandle AppendSingleByte(SnapshotHandle snapshot, byte valueToAdd)
    {
      return mySnapshotPool.AppendBytesToSnapshot(
        snapshot, bytesToAdd: 1, maskToBeAppliedToFirstOfAppendedUint32Elements: valueToAdd);
    }
  }
}