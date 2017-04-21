using System;
using JetBrains.Annotations;

namespace MemorySnapshotPool
{
  public struct ManagedSnapshotStorage : ISnapshotStorage
  {
    private readonly uint myIntsPerSnapshot;
    private uint[] myPoolArray;
    private uint myLastUsedHandle;

    public ManagedSnapshotStorage(uint intsPerSnapshot, uint capacity)
    {
      myIntsPerSnapshot = intsPerSnapshot;
      myPoolArray = new uint[intsPerSnapshot * capacity];
      myLastUsedHandle = 2; // shared + zero
    }

    public uint MemoryConsumptionTotalInBytes
    {
      get { return (uint) (myPoolArray.Length * sizeof(uint)); }
    }

    [Pure]
    private uint[] GetArray(SnapshotHandle snapshot, out uint offset)
    {
      offset = snapshot.Handle * myIntsPerSnapshot;
      return myPoolArray;
    }

    public uint GetUint32(SnapshotHandle snapshot, uint elementIndex)
    {
      return myPoolArray[snapshot.Handle * myIntsPerSnapshot + elementIndex];
    }

    public bool CompareRange(SnapshotHandle snapshot1, SnapshotHandle snapshot2, uint startIndex, uint endIndex)
    {
      uint offset1, offset2;
      var array1 = GetArray(snapshot1, out offset1);
      var array2 = GetArray(snapshot2, out offset2);

      for (var index = startIndex; index < endIndex; index++)
      {
        if (array1[offset1 + index] != array2[offset2 + index]) return false;
      }

      return true;
    }

    public void Copy(SnapshotHandle sourceSnapshot, SnapshotHandle targetSnapshot)
    {
      uint sourceOffset, targetOffset;
      var sourceArray = GetArray(sourceSnapshot, out sourceOffset);
      var targetArray = GetArray(targetSnapshot, out targetOffset);

      Array.Copy(
        sourceArray: sourceArray,
        sourceIndex: sourceOffset,
        destinationArray: targetArray,
        destinationIndex: targetOffset,
        length: myIntsPerSnapshot);
    }

    public void MutateUint32(SnapshotHandle snapshot, uint elementIndex, uint value)
    {
      var offset = snapshot.Handle * myIntsPerSnapshot;
      myPoolArray[offset + elementIndex] = value;
    }

    public SnapshotHandle AllocNewHandle()
    {
      var offset = (myLastUsedHandle + 1) * myIntsPerSnapshot;
      if (offset > myPoolArray.Length)
      {
        Array.Resize(ref myPoolArray, myPoolArray.Length * 2);
      }

      return new SnapshotHandle(myLastUsedHandle++);
    }
  }
}