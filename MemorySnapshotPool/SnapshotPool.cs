using System;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;

namespace MemorySnapshotPool
{
  public class SnapshotPool
  {
    // todo: chunked array or pointers + pinned array
    // todo: migrate to uint32, expose byte and bitvector APIs

    private byte[] myPoolArray;

    private readonly int myBytesPerSnapshot;

    private readonly byte[] mySnapshotArray;

    private int myLastUsedHandle;

    // todo: can store inline in array
    private readonly Dictionary<SnapshotHandle, int> myHandleToHash;

    // todo: can replace with inlined hashtable impl
    private readonly MultiValueDictionary<int, SnapshotHandle> myHashToHandle;

    public SnapshotPool(int bytesPerSnapshot)
    {
      Debug.Assert(bytesPerSnapshot >= 0);

      myPoolArray = new byte[bytesPerSnapshot * 100];
      mySnapshotArray = new byte[bytesPerSnapshot];
      myBytesPerSnapshot = bytesPerSnapshot;

      myLastUsedHandle = 1;
      myHandleToHash = new Dictionary<SnapshotHandle, int> {{ZeroSnapshot, 0}};
      myHashToHandle = new MultiValueDictionary<int, SnapshotHandle> {{0, ZeroSnapshot}};
    }

    public SnapshotHandle ZeroSnapshot
    {
      get { return new SnapshotHandle(0); }
    }

    [Pure, NotNull]
    private byte[] GetArray(SnapshotHandle snapshot, out int shift)
    {
      shift = snapshot.Handle * myBytesPerSnapshot;
      return myPoolArray;
    }

    [Pure]
    public byte GetElementValue(SnapshotHandle snapshot, int elementIndex)
    {
      Debug.Assert(elementIndex >= 0);
      Debug.Assert(elementIndex < myBytesPerSnapshot);

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
      Debug.Assert(elementIndex >= 0);
      Debug.Assert(elementIndex < myBytesPerSnapshot);

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

      IReadOnlyCollection<SnapshotHandle> candidates;
      if (myHashToHandle.TryGetValue(newHash, out candidates))
      {
        foreach (var candidate in candidates)
        {
          if (StructuralEqualsWithChange(sourceArray, sourceShift, candidate, valueToSet, elementIndex))
          {
            return candidate; // already in pool
          }
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
        length: myBytesPerSnapshot);

      newArray[newShift + elementIndex] = valueToSet;

      myHashToHandle.Add(newHash, newHandle);
      myHandleToHash.Add(newHandle, newHash);

      return newHandle;
    }

    [MustUseReturnValue]
    private SnapshotHandle AllocNewHandle()
    {
      var lastIndex = (myLastUsedHandle + 1) * myBytesPerSnapshot;
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

      if (candidateArray[candidateShift + elementIndex] != valueToSet) return false;

      for (var index = elementIndex + 1; index < myBytesPerSnapshot; index++)
      {
        if (sourceArray[sourceShift + index] != candidateArray[candidateShift + index]) return false;
      }

      return true;
    }

    [NotNull, MustUseReturnValue]
    public byte[] ReadToSharedSnapshotArray(SnapshotHandle snapshot)
    {
      int sourceShift;
      var sourceArray = GetArray(snapshot, out sourceShift);

      Array.Copy(
        sourceArray: sourceArray,
        sourceIndex: sourceShift,
        destinationArray: mySnapshotArray,
        destinationIndex: 0,
        length: myBytesPerSnapshot);

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

      IReadOnlyCollection<SnapshotHandle> candidates;
      if (myHashToHandle.TryGetValue(newHash, out candidates))
      {
        foreach (var candidate in candidates)
        {
          if (StructuralEquals(poolArray, 0, candidate))
          {
            return candidate; // already in pool
          }
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
        length: myBytesPerSnapshot);

      myHashToHandle.Add(newHash, newHandle);
      myHandleToHash.Add(newHandle, newHash);

      return newHandle;
    }

    private bool StructuralEquals([NotNull] byte[] sourceArray, int sourceShift, SnapshotHandle candidate)
    {
      int candidateShift;
      var candidateArray = GetArray(candidate, out candidateShift);

      for (var index = 0; index < myBytesPerSnapshot; index++)
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
      return Handle;
    }

    public override string ToString()
    {
      return Handle.ToString();
    }
  }
}