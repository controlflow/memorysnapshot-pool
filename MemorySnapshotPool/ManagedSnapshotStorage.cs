using System;
using JetBrains.Annotations;

namespace MemorySnapshotPool
{
  public struct ManagedSnapshotStorage
  {
    private readonly int myIntsPerSnapshot;
    private uint[] myPoolArray;
    private int myLastUsedHandle;

    public ManagedSnapshotStorage(int intsPerSnapshot)
    {
      myIntsPerSnapshot = intsPerSnapshot;
      myPoolArray = new uint[intsPerSnapshot * 100];
      myLastUsedHandle = 1;
    }

    public int MemoryConsumptionTotalInBytes
    {
      get { return myPoolArray.Length * sizeof(uint); }
    }

    [Pure]
    public uint[] GetArray(SnapshotHandle snapshot, out int shift)
    {
      shift = snapshot.Handle * myIntsPerSnapshot;
      return myPoolArray;
    }

    [Pure]
    public uint GetUint32(SnapshotHandle snapshot, int elementIndex)
    {
      var offset = snapshot.Handle * myIntsPerSnapshot;
      return myPoolArray[offset + elementIndex];
    }

    [Pure]
    public bool CompareRange(SnapshotHandle snapshot1, SnapshotHandle snapshot2, int startIndex, int endIndex)
    {
      int offset1, offset2;
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
      int sourceOffset, targetOffset;
      var sourceArray = GetArray(sourceSnapshot, out sourceOffset);
      var targetArray = GetArray(targetSnapshot, out targetOffset);

      Array.Copy(
        sourceArray: sourceArray,
        sourceIndex: sourceOffset,
        destinationArray: targetArray,
        destinationIndex: targetOffset,
        length: myIntsPerSnapshot);
    }

    public void MutateUint32(SnapshotHandle snapshot, int elementIndex, uint value)
    {
      var offset = snapshot.Handle * myIntsPerSnapshot;
      myPoolArray[offset + elementIndex] = value;
    }

    [MustUseReturnValue]
    public SnapshotHandle AllocNewHandle()
    {
      var lastIndex = (myLastUsedHandle + 1) * myIntsPerSnapshot;
      if (lastIndex > myPoolArray.Length)
      {
        Array.Resize(ref myPoolArray, myPoolArray.Length * 2);
      }

      return new SnapshotHandle(myLastUsedHandle++);
    }
  }
}