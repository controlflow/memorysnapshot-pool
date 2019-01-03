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
      var snapshotSizeInBytes = snapshot.SnapshotSizeInBytes;
      var shiftedMask = (uint) valueToAdd << (int) ((snapshotSizeInBytes % 4) * 8);

      return mySnapshotPool.AppendBytesToSnapshot(
        snapshot, bytesToAdd: 1, maskToBeAppliedToFirstOfAppendedUint32Elements: shiftedMask);
    }

    [NotNull, Pure]
    public byte[] SnapshotToDebugArray(SnapshotHandle snapshot)
    {
      var sizeInBytes = snapshot.SnapshotSizeInBytes;
      var sizeInInts = sizeInBytes / 4 + (sizeInBytes % 4 == 0 ? 0u : 1u);
      var bytes = new byte[sizeInBytes];

      for (uint elementIndex = 0, byteIndex = 0; elementIndex < sizeInInts; elementIndex++)
      {
        var uintValue = mySnapshotPool.GetUint32(snapshot, elementIndex);

        switch (sizeInBytes - byteIndex)
        {
          case 1:
            bytes[byteIndex++] = (byte) uintValue;
            break;

          case 2:
            bytes[byteIndex++] = (byte) uintValue;
            bytes[byteIndex++] = (byte) (uintValue >> 8);
            break;

          case 3:
            bytes[byteIndex++] = (byte) uintValue;
            bytes[byteIndex++] = (byte) (uintValue >> 8);
            bytes[byteIndex++] = (byte) (uintValue >> 16);
            break;

          default:
            bytes[byteIndex++] = (byte) uintValue;
            bytes[byteIndex++] = (byte) (uintValue >> 8);
            bytes[byteIndex++] = (byte) (uintValue >> 16);
            bytes[byteIndex++] = (byte) (uintValue >> 24);
            break;
        }
      }

      return bytes;
    }
  }
}