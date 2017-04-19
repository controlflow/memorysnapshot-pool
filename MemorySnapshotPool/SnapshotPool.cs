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

    #region Storage

    [Pure, NotNull]
    private uint[] GetArray(SnapshotHandle snapshot, out int shift)
    {
      return myStorage.GetArray(snapshot, out shift);
    }

    #endregion

    [Pure]
    public uint GetUint32(SnapshotHandle snapshot, int elementIndex)
    {
      Debug.Assert(elementIndex >= 0);
      Debug.Assert(elementIndex < myIntsPerSnapshotWithoutHash); // loose check

      int shift;
      var array = GetArray(snapshot, out shift);
      return array[shift + elementIndex];
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

      int sourceShift;
      var sourceArray = GetArray(snapshot, out sourceShift);

      var existingValue = sourceArray[sourceShift + elementIndex];
      if (existingValue == valueToSet)
      {
        return snapshot; // the same value
      }

      var currentHash = (int) sourceArray[sourceShift + myIntsPerSnapshotWithoutHash];

      var hashWithoutElement = currentHash ^ HashPart(existingValue, elementIndex);
      var newHash = hashWithoutElement ^ HashPart(valueToSet, elementIndex);

      var withOneValueChanged = new ExistingPoolSnapshotWithOneValueChangedExternalKey(
        this, newHash, sourceArray, sourceShift, valueToSet, elementIndex);

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
      private readonly SnapshotHandle myHandle;

      public ExistingPoolSnapshotExternalKey([NotNull] SnapshotPool pool, SnapshotHandle handle)
      {
        myHandle = handle;
        myPool = pool;
      }

      public bool Equals(SnapshotHandle keyHandle)
      {
        return keyHandle == myHandle;
      }

      public int HashCode()
      {
        int shift;
        var array = myPool.GetArray(myHandle, out shift);

        return (int) array[shift + myPool.myIntsPerSnapshotWithoutHash];
      }
    }

    private struct ExistingPoolSnapshotWithOneValueChangedExternalKey : ExternalKeysHashSet<SnapshotHandle>.IExteralKey
    {
      [NotNull] private readonly SnapshotPool myPool;
      private readonly int myNewHash;
      private readonly uint[] mySourceArray;
      private readonly int mySourceShift;
      private readonly uint myValueToSet;
      private readonly int myElementIndex;

      public ExistingPoolSnapshotWithOneValueChangedExternalKey(
        SnapshotPool pool, int newHash, uint[] sourceArray, int sourceShift, uint valueToSet, int elementIndex)
      {
        myPool = pool;
        myNewHash = newHash;
        mySourceArray = sourceArray;
        mySourceShift = sourceShift;
        myValueToSet = valueToSet;
        myElementIndex = elementIndex;
      }

      public bool Equals(SnapshotHandle candidateHandle)
      {
        int candidateShift;
        var candidateArray = myPool.GetArray(candidateHandle, out candidateShift);

        for (var index = 0; index < myElementIndex; index++)
        {
          if (mySourceArray[mySourceShift + index] != candidateArray[candidateShift + index]) return false;
        }

        if (candidateArray[candidateShift + myElementIndex] != myValueToSet) return false;

        for (var index = myElementIndex + 1; index < myPool.myIntsPerSnapshotWithoutHash; index++)
        {
          if (mySourceArray[mySourceShift + index] != candidateArray[candidateShift + index]) return false;
        }

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

        int newShift;
        var newArray = myPool.GetArray(newHandle, out newShift);
        Array.Copy(
          sourceArray: mySourceArray,
          sourceIndex: mySourceShift,
          destinationArray: newArray,
          destinationIndex: newShift,
          length: myPool.myIntsPerSnapshotWithoutHash);

        newArray[newShift + myElementIndex] = myValueToSet;
        newArray[newShift + myPool.myIntsPerSnapshotWithoutHash] = (uint) myNewHash;

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