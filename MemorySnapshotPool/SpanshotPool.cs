using System;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;

namespace MemorySnapshotPool
{
  public class SpanshotPool
  {
    // todo: chunked array or pointers + pinned array

    private byte[] myPoolArray;
    private readonly byte[] mySecondArray;

    private readonly int myElementPerSnapshot;

    private readonly byte[] mySnapshotArray;

    private int myLastUsedHandle = 1;

    // todo: can store inline in array
    private readonly Dictionary<SnapshotHandle, int> myHandleToHash = new Dictionary<SnapshotHandle, int>();

    // todo: can replace with inlined hashtable impl
    private readonly MultiValueDictionary<int, SnapshotHandle> myHashToHandle = new MultiValueDictionary<int, SnapshotHandle>();

    public SpanshotPool(int elementPerSnapshot)
    {
      myPoolArray = new byte[elementPerSnapshot * 100];
      mySnapshotArray = new byte[elementPerSnapshot];
      myElementPerSnapshot = elementPerSnapshot;
    }

    public SnapshotHandle Initial
    {
      get { return new SnapshotHandle(0); }
    }

    [Pure, NotNull]
    private byte[] GetArray(SnapshotHandle snapshot, out int shift)
    {
      shift = snapshot.Handle * myElementPerSnapshot;
      return myPoolArray;
    }

    [Pure]
    public byte GetElementValue(SnapshotHandle snapshot, int elementIndex)
    {
      Debug.Assert(elementIndex > 0);
      Debug.Assert(elementIndex <= myElementPerSnapshot);

      int shift;
      var array = GetArray(snapshot, out shift);
      return array[shift + elementIndex];
    }

    [Pure]
    private static int HashPart(byte value, int elementIndex)
    {
      var shiftAmount = (elementIndex * 2) % 32;

      var a = value << shiftAmount;
      var b = value >> (32 - shiftAmount);

      return a | b;
    }

    [MustUseReturnValue]
    public SnapshotHandle SetElementValue(SnapshotHandle snapshot, int elementIndex, byte valueToSet)
    {
      Debug.Assert(elementIndex > 0);
      Debug.Assert(elementIndex <= myElementPerSnapshot);

      int sourceShift;
      var sourceArray = GetArray(snapshot, out sourceShift);

      var existingValue = sourceArray[sourceShift + elementIndex];
      if (existingValue == valueToSet)
      {
        return snapshot; // the same value
      }

      var currentHash = myHandleToHash[snapshot];

      var hashWithoutElement = currentHash ^ HashPart(existingValue, elementIndex);
      var newHash = hashWithoutElement ^ HashPart(valueToSet, elementIndex);

      foreach (var candidate in myHashToHandle[newHash])
      {
        if (StructuralEqualsWithChange(sourceArray, sourceShift, candidate, valueToSet, elementIndex))
        {
          return candidate; // already in pool
        }
      }

      var newHandle = AllocNewHandle();

      int newShift;
      var newArray = GetArray(newHandle, out newShift);
      Array.Copy(
        sourceArray: sourceArray,
        sourceIndex: sourceShift,
        destinationArray: newArray,
        destinationIndex: newShift,
        length: myElementPerSnapshot);

      newArray[elementIndex] = valueToSet;

      myHashToHandle.Add(newHash, newHandle);
      myHandleToHash.Add(newHandle, newHash);

      return newHandle;
    }

    [MustUseReturnValue]
    private SnapshotHandle AllocNewHandle()
    {
      var lastIndex = (myLastUsedHandle + 1) * myElementPerSnapshot;
      if (lastIndex > myPoolArray.Length)
      {
        Array.Resize(ref myPoolArray, myPoolArray.Length * 2);
      }

      return new SnapshotHandle(myLastUsedHandle++);
    }

    private bool StructuralEqualsWithChange([NotNull] byte[] sourceArray, int sourceShift, SnapshotHandle candidate, byte valueToSet, int elementIndex)
    {
      int candidateShift;
      var candidateArray = GetArray(candidate, out candidateShift);

      for (var index = 0; index < elementIndex; index++)
      {
        if (sourceArray[sourceShift + index] != candidateArray[candidateShift + index]) return false;
      }

      if (candidateArray[elementIndex] != valueToSet) return false;

      for (var index = elementIndex + 1; index < myElementPerSnapshot; index++)
      {
        if (sourceArray[sourceShift + index] != candidateArray[candidateShift + index]) return false;
      }

      return true;
    }

    [NotNull, MustUseReturnValue]
    public byte[] ReadSharedSnapshotArray(SnapshotHandle snapshot)
    {
      int sourceShift;
      var sourceArray = GetArray(snapshot, out sourceShift);

      Array.Copy(
        sourceArray: sourceArray,
        sourceIndex: sourceShift,
        destinationArray: mySnapshotArray,
        destinationIndex: 0,
        length: myElementPerSnapshot);

      return mySnapshotArray;
    }

    [MustUseReturnValue]
    public SnapshotHandle SetSharedSnapshotArray()
    {
      var poolArray = myPoolArray;
      var newHash = 0;

      for (var index = 0; index < poolArray.Length; index++)
      {
        newHash ^= HashPart(poolArray[index], index);
      }

      foreach (var candidate in myHashToHandle[newHash])
      {
        if (StructuralEquals(poolArray, 0, candidate))
        {
          return candidate; // already in pool
        }
      }

      var newHandle = AllocNewHandle();

      int newShift;
      var newArray = GetArray(newHandle, out newShift);
      Array.Copy(
        sourceArray: poolArray,
        sourceIndex: 0,
        destinationArray: newArray,
        destinationIndex: newShift,
        length: myElementPerSnapshot);

      myHashToHandle.Add(newHash, newHandle);
      myHandleToHash.Add(newHandle, newHash);

      return newHandle;
    }

    private bool StructuralEquals([NotNull] byte[] sourceArray, int sourceShift, SnapshotHandle candidate)
    {
      int candidateShift;
      var candidateArray = GetArray(candidate, out candidateShift);

      for (var index = 0; index < myElementPerSnapshot; index++)
      {
        if (sourceArray[sourceShift + index] != candidateArray[candidateShift + index]) return false;
      }

      return true;
    }
  }

  public struct SnapshotHandle : IEquatable<SnapshotHandle>
  {
    public readonly int Handle;

    public SnapshotHandle(int handle)
    {
      Handle = handle;
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
      return Handle;
    }
  }
}