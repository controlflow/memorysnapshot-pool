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

    public SnapshotPool(int bytesPerSnapshot)
    {
      Debug.Assert(bytesPerSnapshot >= 0, "bytesPerSnapshot >= 0");

      var snapshotSize = sizeof(int) + bytesPerSnapshot;
      myIntsPerSnapshot = (snapshotSize / sizeof(uint)) + (snapshotSize % sizeof(uint) == 0 ? 0 : 1);
      myIntsPerSnapshotWithoutHash = myIntsPerSnapshot - 1;

      myStorage = new ManagedSnapshotStorage(myIntsPerSnapshot);

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
      public bool Equals(SnapshotHandle candidateSnapshot)
      {
        throw new InvalidOperationException();
      }

      public int HashCode() { return 0; }
    }

    private static readonly SnapshotHandle SharedSnapshot = new SnapshotHandle(0);
    public static readonly SnapshotHandle ZeroSnapshot = new SnapshotHandle(1);

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

      var withOneValueChanged = new ExistingPoolSnapshotWithOneValueChangedExternalKey(this, snapshot, newHash, valueToSet, elementIndex);

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

      public bool Equals(SnapshotHandle candidateSnapshot)
      {
        return candidateSnapshot == mySnapshot;
      }

      public int HashCode()
      {
        return (int) myPool.myStorage.GetUint32(mySnapshot, myPool.myIntsPerSnapshotWithoutHash);
      }
    }

    private struct ExistingPoolSnapshotWithOneValueChangedExternalKey : ExternalKeysHashSet<SnapshotHandle>.IExteralKey
    {
      [NotNull] private readonly SnapshotPool myPool;
      private readonly SnapshotHandle mySourceSnapshot;
      private readonly int myNewHash;
      private readonly uint myValueToSet;
      private readonly int myElementIndex;

      public ExistingPoolSnapshotWithOneValueChangedExternalKey(
        SnapshotPool pool, SnapshotHandle sourceSnapshot, int newHash, uint valueToSet, int elementIndex)
      {
        myPool = pool;
        mySourceSnapshot = sourceSnapshot;
        myNewHash = newHash;
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

        myPool.myStorage.Copy(mySourceSnapshot, newHandle);
        myPool.myStorage.MutateUint32(newHandle, myElementIndex, myValueToSet);
        myPool.myStorage.MutateUint32(newHandle, myPool.myIntsPerSnapshotWithoutHash, (uint) myNewHash);

        myPool.myExistingSnapshots.Add(newHandle, new ExistingPoolSnapshotExternalKey(myPool, newHandle));

        return newHandle;
      }
    }

    public void CopyToSharedSnapshot(SnapshotHandle snapshot)
    {
      myStorage.Copy(snapshot, SharedSnapshot);
    }

    [NotNull, MustUseReturnValue]
    public uint[] SharedSnapshotToArray(SnapshotHandle snapshot)
    {
      CopyToSharedSnapshot(snapshot);

      var array = new uint[myIntsPerSnapshotWithoutHash];
      for (var index = 0; index < myIntsPerSnapshotWithoutHash; index++)
      {
        array[index] = myStorage.GetUint32(snapshot, index);
      }

      return array;
    }

    public void SetSharedSnapshotUint32(int elementIndex, uint valueToSet)
    {
      var existingValue = myStorage.GetUint32(SharedSnapshot, elementIndex);
      var sharedHash = (int) myStorage.GetUint32(SharedSnapshot, myIntsPerSnapshotWithoutHash);

      var hashWithoutElement = sharedHash ^ HashPart(existingValue, elementIndex);
      var newHash = hashWithoutElement ^ HashPart(valueToSet, elementIndex);

      myStorage.MutateUint32(SharedSnapshot, elementIndex, valueToSet);
      myStorage.MutateUint32(SharedSnapshot, myIntsPerSnapshotWithoutHash, (uint) newHash);
    }

    [MustUseReturnValue]
    public SnapshotHandle GetOrAppendSharedSnapshot()
    {
      SnapshotHandle existingHandle;
      var sharedArrayExternalKey = new SharedSnapshotExternalKey(this);

      if (myExistingSnapshots.TryGetKey(sharedArrayExternalKey, out existingHandle))
      {
        return existingHandle;
      }

      return sharedArrayExternalKey.AllocateChanged();
    }

    private struct SharedSnapshotExternalKey : ExternalKeysHashSet<SnapshotHandle>.IExteralKey
    {
      [NotNull] private readonly SnapshotPool myPool;

      public SharedSnapshotExternalKey([NotNull] SnapshotPool pool)
      {
        myPool = pool;
      }

      public bool Equals(SnapshotHandle candidateSnapshot)
      {
        return myPool.myStorage.CompareRange(candidateSnapshot, SharedSnapshot, 0, myPool.myIntsPerSnapshotWithoutHash);
      }

      public int HashCode()
      {
        return (int) myPool.myStorage.GetUint32(SharedSnapshot, myPool.myIntsPerSnapshotWithoutHash);
      }

      [MustUseReturnValue]
      public SnapshotHandle AllocateChanged()
      {
        var newHandle = myPool.myStorage.AllocNewHandle();
        myPool.myStorage.Copy(SharedSnapshot, newHandle);
        myPool.myExistingSnapshots.Add(newHandle, new ExistingPoolSnapshotExternalKey(myPool, newHandle));

        return newHandle;
      }
    }
  }
}