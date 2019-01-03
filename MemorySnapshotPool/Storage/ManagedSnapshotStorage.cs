using System;
using JetBrains.Annotations;

namespace MemorySnapshotPool.Storage
{
  public struct ManagedSnapshotStorage : ISnapshotStorage
  {
    private uint[] myPoolArray;
    private uint myLastUsedOffset;

    public void Initialize(uint capacityInInts)
    {
      if (myPoolArray != null)
        throw new InvalidOperationException("Already initialized");
      
      myPoolArray = new uint[capacityInInts];
      myLastUsedOffset = 0;
    }

    public uint MemoryConsumptionTotalInBytes
    {
      get
      {
        if (myPoolArray == null) return 0;

        return (uint) (myPoolArray.Length * sizeof(uint));
      }
    }

    [Pure]
    private uint[] GetArray(SnapshotHandle snapshot, out uint offset)
    {
      offset = snapshot.Handle;
      return myPoolArray;
    }

    public uint GetUint32(uint offset, uint index)
    {
      return myPoolArray[offset + index];
    }

    public bool CompareRange(SnapshotHandle snapshot1, SnapshotHandle snapshot2, uint startIndex, uint endIndex)
    {
      var array1 = GetArray(snapshot1, out var offset1);
      var array2 = GetArray(snapshot2, out var offset2);

      for (var index = startIndex; index < endIndex; index++)
      {
        if (array1[offset1 + index] != array2[offset2 + index]) return false;
      }

      return true;
    }

    public void Copy(SnapshotHandle sourceSnapshot, SnapshotHandle targetSnapshot, uint intsToCopy)
    {
      var sourceArray = GetArray(sourceSnapshot, out var sourceOffset);
      var targetArray = GetArray(targetSnapshot, out var targetOffset);

      Array.Copy(
        sourceArray: sourceArray,
        sourceIndex: sourceOffset,
        destinationArray: targetArray,
        destinationIndex: targetOffset,
        length: intsToCopy);
    }

    public void MutateUint32(SnapshotHandle snapshot, uint elementIndex, uint value)
    {
      myPoolArray[snapshot.Handle + elementIndex] = value;
    }

    public SnapshotHandle AllocateNewHandle(uint intsToAllocate)
    {
      var lastOffsetUsed = myLastUsedOffset;
      
      var newLastOffset = lastOffsetUsed + intsToAllocate;
      if (newLastOffset > myPoolArray.Length)
      {
        Array.Resize(ref myPoolArray, myPoolArray.Length * 2);
      }

      myLastUsedOffset = newLastOffset;
      return new SnapshotHandle(lastOffsetUsed);
    }
  }
}