using System;
using System.Diagnostics;
using JetBrains.Annotations;

namespace MemorySnapshotPool
{
  public class SnapshotPool
  {
    private readonly int myIntsPerSnapshot;
    private readonly int myIntsPerSnapshotWithoutHash;

    private ExternalKeysHashSet<SnapshotHandle> myExistingSnapshots;
    private ManagedSnapshotStorage myStorage;

    // shared array
    private readonly uint[] mySharedSnapshotArray;
    private int mySharedSnapshotHash;

    public SnapshotPool(int bytesPerSnapshot)
    {
      Debug.Assert(bytesPerSnapshot >= 0, "bytesPerSnapshot >= 0");

      var snapshotSize = sizeof(int) + bytesPerSnapshot;
      myIntsPerSnapshot = (snapshotSize / sizeof(uint)) + (snapshotSize % sizeof(uint) == 0 ? 0 : 1);
      myIntsPerSnapshotWithoutHash = myIntsPerSnapshot - 1;

      // storage system:
      myStorage = new ManagedSnapshotStorage(myIntsPerSnapshot);

      // todo: get rid of
      mySharedSnapshotArray = new uint[myIntsPerSnapshotWithoutHash];

      myExistingSnapshots = new ExternalKeysHashSet<SnapshotHandle>();
      myExistingSnapshots.Add(ZeroSnapshot, new ZeroSnapshotExternalKey());
    }

    public int MemoryConsumptionPerSnapshotInBytes
    {
      // todo: ExternalKeysHashSet
      get { return myIntsPerSnapshot / sizeof(uint); }
    }

    public int MemoryConsumptionTotalInBytes
    {
      // todo: ExternalKeysHashSet
      get { return myStorage.MemoryConsumptionTotalInBytes; }
    }

    // todo: snapshots count
    // todo: fill ratio

    private struct ZeroSnapshotExternalKey : ExternalKeysHashSet<SnapshotHandle>.IExteralKey
    {
      public bool Equals(SnapshotHandle keyHandle)
      {
        throw new InvalidOperationException();
      }

      public int HashCode() { return 0; }
    }

    public SnapshotHandle ZeroSnapshot
    {
      get { return new SnapshotHandle(0); }
    }

    //public SnapshotHandle SharedSnapshot
    //{
    //  get { }
    //}

    #region Storage

    [Pure, NotNull, Obsolete]
    private uint[] GetArray(SnapshotHandle snapshot, out int shift)
    {
      return myStorage.GetArray(snapshot, out shift);
    }

    #endregion

    [Pure]
    public uint GetUint32(SnapshotHandle snapshot, int elementIndex)
    {
      Debug.Assert(elementIndex >= 0);
      Debug.Assert(elementIndex < myIntsPerSnapshotWithoutHash);

      return myStorage.GetUint32(snapshot, elementIndex);
    }

    [Pure]
    private static int HashPart(uint value, int elementIndex)
    {
      var shiftAmount = (elementIndex * 2) % 32;

      var a = value << shiftAmount;
      var b = value >> (32 - shiftAmount);

      return (int) (a | b);
    }

    [MustUseReturnValue]
    public SnapshotHandle SetUInt32(SnapshotHandle snapshot, int elementIndex, uint valueToSet)
    {
      Debug.Assert(elementIndex >= 0);
      Debug.Assert(elementIndex < myIntsPerSnapshotWithoutHash);

      var existingValue = myStorage.GetUint32(snapshot, elementIndex);
      if (existingValue == valueToSet)
      {
        return snapshot; // the same value
      }

      var currentHash = (int) myStorage.GetUint32(snapshot, myIntsPerSnapshotWithoutHash);

      var hashWithoutElement = currentHash ^ HashPart(existingValue, elementIndex);
      var newHash = hashWithoutElement ^ HashPart(valueToSet, elementIndex);

      var withOneValueChanged = new ExistingPoolSnapshotWithOneValueChangedExternalKey(this, newHash, snapshot, valueToSet, elementIndex);

      SnapshotHandle existingHandle;
      if (myExistingSnapshots.TryGetKey(withOneValueChanged, out existingHandle))
      {
        return existingHandle;
      }

      return withOneValueChanged.AllocateChanged();
    }

    private struct ExistingPoolSnapshotExternalKey : ExternalKeysHashSet<SnapshotHandle>.IExteralKey
    {
      [NotNull] private readonly SnapshotPool myPool;
      private readonly SnapshotHandle mySnapshot;

      public ExistingPoolSnapshotExternalKey([NotNull] SnapshotPool pool, SnapshotHandle snapshot)
      {
        mySnapshot = snapshot;
        myPool = pool;
      }

      public bool Equals(SnapshotHandle keyHandle)
      {
        return keyHandle == mySnapshot;
      }

      public int HashCode()
      {
        return (int) myPool.myStorage.GetUint32(mySnapshot, myPool.myIntsPerSnapshotWithoutHash);
      }
    }

    private struct ExistingPoolSnapshotWithOneValueChangedExternalKey : ExternalKeysHashSet<SnapshotHandle>.IExteralKey
    {
      [NotNull] private readonly SnapshotPool myPool;
      private readonly int myNewHash;
      private readonly SnapshotHandle mySourceSnapshot;
      private readonly uint myValueToSet;
      private readonly int myElementIndex;

      public ExistingPoolSnapshotWithOneValueChangedExternalKey(
        SnapshotPool pool, int newHash, SnapshotHandle sourceSnapshot, uint valueToSet, int elementIndex)
      {
        myPool = pool;
        myNewHash = newHash;
        mySourceSnapshot = sourceSnapshot;
        myValueToSet = valueToSet;
        myElementIndex = elementIndex;
      }

      public bool Equals(SnapshotHandle candidateSnapshot)
      {
        var storage = myPool.myStorage;
        if (!storage.CompareRange(mySourceSnapshot, candidateSnapshot,
                                  startIndex: 0, endIndex: myElementIndex)) return false;

        if (storage.GetUint32(candidateSnapshot, myElementIndex) != myValueToSet) return false;

        if (!storage.CompareRange(mySourceSnapshot, candidateSnapshot,
                                  startIndex: myElementIndex + 1,
                                  endIndex: myPool.myIntsPerSnapshotWithoutHash)) return false;

        return true;
      }

      public int HashCode()
      {
        return myNewHash;
      }

      [MustUseReturnValue]
      public SnapshotHandle AllocateChanged()
      {
        var newHandle = myPool.myStorage.AllocNewHandle();

        myPool.myStorage.CopyFrom(mySourceSnapshot, newHandle);
        myPool.myStorage.MutateUint32(newHandle, myElementIndex, myValueToSet);
        myPool.myStorage.MutateUint32(newHandle, myPool.myIntsPerSnapshotWithoutHash, (uint) myNewHash);

        myPool.myExistingSnapshots.Add(newHandle, new ExistingPoolSnapshotExternalKey(myPool, newHandle));

        return newHandle;
      }
    }

    [NotNull, MustUseReturnValue]
    public uint[] ReadToSharedSnapshotArray(SnapshotHandle snapshot)
    {
      int sourceShift;
      var sourceArray = GetArray(snapshot, out sourceShift);

      Array.Copy(
        sourceArray: sourceArray,
        sourceIndex: sourceShift,
        destinationArray: mySharedSnapshotArray,
        destinationIndex: 0,
        length: myIntsPerSnapshotWithoutHash);

      mySharedSnapshotHash = (int) sourceArray[sourceShift + myIntsPerSnapshotWithoutHash];

      return mySharedSnapshotArray;
    }

    public void SetSharedSnapshotElement(int elementIndex, byte valueToSet)
    {
      var existingValue = mySharedSnapshotArray[elementIndex];

      var hashWithoutElement = mySharedSnapshotHash ^ HashPart(existingValue, elementIndex);
      mySharedSnapshotHash = hashWithoutElement ^ HashPart(valueToSet, elementIndex);

      mySharedSnapshotArray[elementIndex] = valueToSet;
    }

    [MustUseReturnValue]
    public SnapshotHandle SetModifiedSharedSnapshotArray()
    {
      SnapshotHandle existingHandle;
      var sharedArrayExternalKey = new SharedArrayExternalKey(this, mySharedSnapshotHash);

      if (myExistingSnapshots.TryGetKey(sharedArrayExternalKey, out existingHandle))
      {
        return existingHandle;
      }

      return sharedArrayExternalKey.AllocateChanged();
    }

    private struct SharedArrayExternalKey : ExternalKeysHashSet<SnapshotHandle>.IExteralKey
    {
      private readonly SnapshotPool myPool;
      private readonly int mySharedSnapshotHash;

      public SharedArrayExternalKey(SnapshotPool pool, int sharedSnapshotHash)
      {
        myPool = pool;
        mySharedSnapshotHash = sharedSnapshotHash;
      }

      public bool Equals(SnapshotHandle keyHandle)
      {
        int candidateShift;
        var candidateArray = myPool.GetArray(keyHandle, out candidateShift);

        for (var index = 0; index < myPool.myIntsPerSnapshotWithoutHash; index++)
        {
          if (myPool.mySharedSnapshotArray[index] != candidateArray[candidateShift + index]) return false;
        }

        return true;
      }

      public int HashCode()
      {
        return mySharedSnapshotHash;
      }

      [MustUseReturnValue]
      public SnapshotHandle AllocateChanged()
      {
        var newHandle = myPool.myStorage.AllocNewHandle();

        int newShift;
        var newArray = myPool.GetArray(newHandle, out newShift);
        Array.Copy(
          sourceArray: myPool.mySharedSnapshotArray,
          sourceIndex: 0,
          destinationArray: newArray,
          destinationIndex: newShift,
          length: myPool.myIntsPerSnapshotWithoutHash);

        newArray[newShift + myPool.myIntsPerSnapshotWithoutHash] = (uint) mySharedSnapshotHash;

        myPool.myExistingSnapshots.Add(newHandle, new ExistingPoolSnapshotExternalKey(myPool, newHandle));

        return newHandle;
      }
    }

    [MustUseReturnValue]
    public SnapshotHandle SetWholeSharedSnapshotArray()
    {
      var snapshotArray = mySharedSnapshotArray;
      var computedHash = 0;

      for (var index = 0; index < snapshotArray.Length; index++)
      {
        computedHash ^= HashPart(snapshotArray[index], index);
      }

      SnapshotHandle existingHandle;
      var sharedArrayExternalKey = new SharedArrayExternalKey(this, computedHash);

      if (myExistingSnapshots.TryGetKey(sharedArrayExternalKey, out existingHandle))
      {
        return existingHandle;
      }

      return sharedArrayExternalKey.AllocateChanged();
    }
  }

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

    public void CopyFrom(SnapshotHandle sourceSnapshot, SnapshotHandle targetSnapshot)
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