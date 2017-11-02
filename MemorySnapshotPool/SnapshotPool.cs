using System;
using System.Diagnostics;
using JetBrains.Annotations;

namespace MemorySnapshotPool
{
  public class SnapshotPool
  {
    private readonly uint myIntsPerSnapshot;
    private readonly uint myIntsPerSnapshotWithoutHash;

    private ExternalKeysHashSet<SnapshotHandle> myExistingSnapshots;
    private ManagedSnapshotStorage myStorage;
    //private UnmanagedSnapshotStorage myStorage;
    private uint myTrivialSharedSnapshotContent;

    public SnapshotPool(uint bytesPerSnapshot, uint capacity = 100)
    {
      var snapshotSize = sizeof(int) + bytesPerSnapshot;
      myIntsPerSnapshot = (snapshotSize / sizeof(uint)) + (snapshotSize % sizeof(uint) == 0 ? 0 : 1u);
      myIntsPerSnapshotWithoutHash = myIntsPerSnapshot - 1;

      Debug.Assert(myIntsPerSnapshot <= 84, "myIntsPerSnapshot <= 84");

      if (bytesPerSnapshot > 4)
      {
        //myStorage = new UnmanagedSnapshotStorage(myIntsPerSnapshot, capacity + 2);
        myStorage = new ManagedSnapshotStorage(myIntsPerSnapshot, capacity + 2);

        myExistingSnapshots = new ExternalKeysHashSet<SnapshotHandle>((int)(capacity + 1));
        myExistingSnapshots.Add(ZeroSnapshot, new ZeroSnapshotExternalKey());
      }
    }

    public uint MemoryConsumptionPerSnapshotInBytes
    {
      // todo: add ExternalKeysHashSet
      get { return myIntsPerSnapshot / sizeof(uint); }
    }

    public uint MemoryConsumptionTotalInBytes
    {
      // todo: add ExternalKeysHashSet
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

      public uint HashCode() { return 0; }
    }

    public static readonly SnapshotHandle ZeroSnapshot = new SnapshotHandle(0);
    public static readonly SnapshotHandle SharedSnapshot = new SnapshotHandle(1);

    [Pure]
    public uint GetUint32(SnapshotHandle snapshot, uint elementIndex)
    {
      Debug.Assert(elementIndex < myIntsPerSnapshotWithoutHash);

      if (myIntsPerSnapshotWithoutHash == 1)
      {
        return snapshot == SharedSnapshot ? myTrivialSharedSnapshotContent : snapshot.Handle;
      }

      return myStorage.GetUint32(snapshot, elementIndex);
    }

    [Pure]
    private static uint HashPart(uint value, uint elementIndex)
    {
      var prime = NthPrime(elementIndex);

      return value * prime;
    }

    public static uint NthPrime(uint n)
    {
      switch (n)
      {
        case 0: return 3;
        case 1: return 5;
        case 2: return 7;
        case 3: return 11;
        case 4: return 13;
        case 5: return 17;
        case 6: return 19;
        case 7: return 23;
        case 8: return 29;
        case 9: return 31;
        case 10: return 37;
        case 11: return 41;
        case 12: return 43;
        case 13: return 47;
        case 14: return 53;
        case 15: return 59;
        case 16: return 61;
        case 17: return 67;
        case 18: return 71;
        case 19: return 73;
        case 20: return 79;
        case 21: return 83;
        case 22: return 89;
        case 23: return 97;
        case 24: return 101;
        case 25: return 103;
        case 26: return 107;
        case 27: return 109;
        case 28: return 113;
        case 29: return 127;
        case 30: return 131;
        case 31: return 137;
        case 32: return 139;
        case 33: return 149;
        case 34: return 151;
        case 35: return 157;
        case 36: return 163;
        case 37: return 167;
        case 38: return 173;
        case 39: return 179;
        case 40: return 181;
        case 41: return 191;
        case 42: return 193;
        case 43: return 197;
        case 44: return 199;
        case 45: return 211;
        case 46: return 223;
        case 47: return 227;
        case 48: return 229;
        case 49: return 233;
        case 50: return 239;
        case 51: return 241;
        case 52: return 251;
        case 53: return 257;
        case 54: return 263;
        case 55: return 269;
        case 56: return 271;
        case 57: return 277;
        case 58: return 281;
        case 59: return 283;
        case 60: return 293;
        case 61: return 307;
        case 62: return 311;
        case 63: return 313;
        case 64: return 317;
        case 65: return 331;
        case 66: return 337;
        case 67: return 347;
        case 68: return 349;
        case 69: return 353;
        case 70: return 359;
        case 71: return 367;
        case 72: return 373;
        case 73: return 379;
        case 74: return 383;
        case 75: return 389;
        case 76: return 397;
        case 77: return 401;
        case 78: return 409;
        case 79: return 419;
        case 80: return 421;
        case 81: return 431;
        case 82: return 433;
        case 83: return 439;
        case 84: return 443;
        default: return 0;
      }
    }

    //[MustUseReturnValue]
    //protected SnapshotHandle SetUnobservedUInt32(SnapshotHandle snapshot, uint elementIndex, uint valueToSet)
    //{
    //  
    //}

    [MustUseReturnValue]
    public SnapshotHandle SetSingleUInt32(SnapshotHandle snapshot, uint elementIndex, uint valueToSet)
    {
      Debug.Assert(elementIndex < myIntsPerSnapshotWithoutHash);

      if (myIntsPerSnapshotWithoutHash == 1)
      {
        return new SnapshotHandle(valueToSet);
      }

      var existingValue = myStorage.GetUint32(snapshot, elementIndex);
      if (existingValue == valueToSet)
      {
        return snapshot; // the same value
      }

      var currentHash = myStorage.GetUint32(snapshot, myIntsPerSnapshotWithoutHash);

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

      public uint HashCode()
      {
        return myPool.myStorage.GetUint32(mySnapshot, myPool.myIntsPerSnapshotWithoutHash);
      }
    }

    private struct ExistingPoolSnapshotWithOneValueChangedExternalKey : ExternalKeysHashSet<SnapshotHandle>.IExteralKey
    {
      [NotNull] private readonly SnapshotPool myPool;
      private readonly SnapshotHandle mySourceSnapshot;
      private readonly uint myNewHash;
      private readonly uint myValueToSet;
      private readonly uint myElementIndex;

      public ExistingPoolSnapshotWithOneValueChangedExternalKey(
        SnapshotPool pool, SnapshotHandle sourceSnapshot, uint newHash, uint valueToSet, uint elementIndex)
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

      public uint HashCode()
      {
        return myNewHash;
      }

      [MustUseReturnValue]
      public SnapshotHandle AllocateChanged()
      {
        var newHandle = myPool.myStorage.AllocateNewHandle();

        myPool.myStorage.Copy(mySourceSnapshot, newHandle);
        myPool.myStorage.MutateUint32(newHandle, myElementIndex, myValueToSet);
        myPool.myStorage.MutateUint32(newHandle, myPool.myIntsPerSnapshotWithoutHash, myNewHash);

        myPool.myExistingSnapshots.Add(newHandle, new ExistingPoolSnapshotExternalKey(myPool, newHandle));

        return newHandle;
      }
    }

    public void LoadToSharedSnapshot(SnapshotHandle snapshot)
    {
      Debug.Assert(snapshot != SharedSnapshot, "snapshot != SharedSnapshot");

      if (myIntsPerSnapshotWithoutHash == 1)
      {
        myTrivialSharedSnapshotContent = snapshot.Handle;
      }
      else
      {
        myStorage.Copy(snapshot, SharedSnapshot);
      }
    }

    [NotNull, Pure]
    public uint[] SnapshotToDebugArray(SnapshotHandle snapshot)
    {
      if (myIntsPerSnapshotWithoutHash == 1)
      {
        return new[] { snapshot.Handle };
      }

      var array = new uint[myIntsPerSnapshotWithoutHash];
      for (var index = 0u; index < myIntsPerSnapshotWithoutHash; index++)
      {
        array[index] = myStorage.GetUint32(snapshot, index);
      }

      return array;
    }

    public void SetSharedSnapshotUint32(uint elementIndex, uint valueToSet)
    {
      Debug.Assert(elementIndex < myIntsPerSnapshotWithoutHash);

      if (myIntsPerSnapshotWithoutHash == 1)
      {
        myTrivialSharedSnapshotContent = valueToSet;
      }
      else
      {
        var existingValue = myStorage.GetUint32(SharedSnapshot, elementIndex);
        var sharedHash = myStorage.GetUint32(SharedSnapshot, myIntsPerSnapshotWithoutHash);

        var hashWithoutElement = sharedHash ^ HashPart(existingValue, elementIndex);
        var newHash = hashWithoutElement ^ HashPart(valueToSet, elementIndex);

        myStorage.MutateUint32(SharedSnapshot, elementIndex, valueToSet);
        myStorage.MutateUint32(SharedSnapshot, myIntsPerSnapshotWithoutHash, newHash);
      }
    }

    [MustUseReturnValue]
    public SnapshotHandle StoreSharedSnapshot()
    {
      if (myIntsPerSnapshotWithoutHash == 1)
      {
        return new SnapshotHandle(myTrivialSharedSnapshotContent);
      }

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

      public uint HashCode()
      {
        return myPool.myStorage.GetUint32(SharedSnapshot, myPool.myIntsPerSnapshotWithoutHash);
      }

      [MustUseReturnValue]
      public SnapshotHandle AllocateChanged()
      {
        var newHandle = myPool.myStorage.AllocateNewHandle();
        myPool.myStorage.Copy(SharedSnapshot, newHandle);
        myPool.myExistingSnapshots.Add(newHandle, new ExistingPoolSnapshotExternalKey(myPool, newHandle));

        return newHandle;
      }
    }
  }
}