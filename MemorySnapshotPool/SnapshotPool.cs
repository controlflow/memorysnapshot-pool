#define VERIFY_POOL_USAGE
using System;
using System.Diagnostics;
using JetBrains.Annotations;
using MemorySnapshotPool.Storage;

namespace MemorySnapshotPool
{
  public class SnapshotPool : SnapshotPool<ManagedSnapshotStorage>
  {
    public SnapshotPool(uint? bytesPerSnapshot = null, uint capacity = 100)
      : base(new ManagedSnapshotStorage(), bytesPerSnapshot, capacity) { }
  }
  
  public class SnapshotPool<TSnapshotStorage>
    where TSnapshotStorage : struct, ISnapshotStorage
  {
    // do not make readonly
    private ExternalKeysHashSet<SnapshotHandle> myExistingSnapshots;
    private TSnapshotStorage myStorage;

    private readonly uint myIntsPerSnapshotWithHash; // todo: get rid of it?
    private readonly uint myIntsPerSnapshotWithoutHash;

    private const uint VariableSizeSnapshot = uint.MaxValue;
    private const uint BytesOfVariableSnapshotHandleCanStoreInline = 3;

    private int mySharedSnapshotSize = -1;
    private SnapshotHandle mySharedSnapshot;

    protected SnapshotPool(TSnapshotStorage snapshotStorage, uint? bytesPerSnapshot = null, uint capacity = 100)
    {
      myStorage = snapshotStorage;
      
      if (bytesPerSnapshot != null)
      {
        var snapshotSize = sizeof(uint) + bytesPerSnapshot.Value;
        myIntsPerSnapshotWithHash = (snapshotSize / sizeof(uint)) + (snapshotSize % sizeof(uint) == 0 ? 0 : 1u);
        myIntsPerSnapshotWithoutHash = myIntsPerSnapshotWithHash - 1;
        
        if (myIntsPerSnapshotWithoutHash > 84)
          throw new ArgumentOutOfRangeException(message: "Too many elements per snapshot", null);
      }
      else
      { 
        myIntsPerSnapshotWithHash = VariableSizeSnapshot;
        myIntsPerSnapshotWithoutHash = VariableSizeSnapshot;
      }

      if (bytesPerSnapshot > 4)
      {
        myExistingSnapshots = new ExternalKeysHashSet<SnapshotHandle>((int) (capacity + 1));
        myExistingSnapshots.Add(SnapshotHandle.Zero, new ZeroSnapshotExternalKey());

        const uint specialSnapshotsCount = 2;
        myStorage.Initialize(myIntsPerSnapshotWithHash * (capacity + specialSnapshotsCount));
        
        var zero = myStorage.AllocateNewHandle(myIntsPerSnapshotWithHash);
        if (zero.Handle != 0)
          throw new ArgumentException("Something is deeply wrong with storage initialization");

        // unmanaged storage is not zeroed
        for (uint index = 0; index < myIntsPerSnapshotWithHash; index++)
        {
          myStorage.MutateUint32(zero, index, value: 0);
        }
      }
      else if (bytesPerSnapshot == null)
      {
        myExistingSnapshots = new ExternalKeysHashSet<SnapshotHandle>((int) capacity);
        myStorage.Initialize(capacity);
      }
    }

    [Conditional("VERIFY_POOL_USAGE")]
    private static void Assert(bool condition, string message)
    {
      if (!condition)
        throw new ArgumentException(message);
    }

    public uint MemoryConsumptionPerSnapshotInBytes
    {
      get
      {
        if (myIntsPerSnapshotWithoutHash == 1) return 0;

        return myIntsPerSnapshotWithHash * sizeof(uint) + myExistingSnapshots.BytesPerRecord;
      }
    }

    public uint MemoryConsumptionTotalInBytes => myStorage.MemoryConsumptionTotalInBytes + myExistingSnapshots.TotalBytes;

    // todo: snapshots count
    // todo: fill ratio

    private readonly struct ZeroSnapshotExternalKey : ExternalKeysHashSet<SnapshotHandle>.IExternalKey
    {
      public bool Equals(SnapshotHandle candidateSnapshot) => throw new InvalidOperationException();

      public uint HashCode() => 0;
    }

    [Pure]
    public uint GetUint32(SnapshotHandle snapshot, uint elementIndex)
    {
      switch (myIntsPerSnapshotWithoutHash)
      {
        case 1:
        {
          Assert(elementIndex == 0, "Element index is out of range");
          return snapshot.Handle; // inline content
        }

        case VariableSizeSnapshot:
        {
          var sizeInBytes = snapshot.SnapshotSizeInBytes;
          Assert(elementIndex * 4 < sizeInBytes, "Element index is out of range");

          if (sizeInBytes <= BytesOfVariableSnapshotHandleCanStoreInline) // inline content
            return snapshot.SnapshotHandleWithoutSize;

          return myStorage.GetUint32(snapshot.SnapshotHandleWithoutSize, elementIndex);
        }

        default:
        {
          Assert(elementIndex < myIntsPerSnapshotWithoutHash, "Element index is out of range");

          return myStorage.GetUint32(snapshot.Handle, elementIndex);
        }
      }
    }

    [Pure]
    private static uint HashPart(uint value, uint elementIndex)
    {
      var prime = NthPrime(elementIndex);

      return value * prime;
    }

    [Pure]
    private static uint NthPrime(uint n)
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

    [MustUseReturnValue]
    public SnapshotHandle SetSingleUInt32(SnapshotHandle snapshot, uint elementIndex, uint valueToSet)
    {
      uint sourceOffset;

      switch (myIntsPerSnapshotWithoutHash)
      {
        case 1:
        {
          Assert(elementIndex == 0, "Element index is out of range");
          return new SnapshotHandle(valueToSet);
        }

        case VariableSizeSnapshot:
        {
          var sizeInBytes = snapshot.SnapshotSizeInBytes;
          Assert(elementIndex * 4 < sizeInBytes, "Element index is out of range");

          valueToSet &= ~(uint.MaxValue << (int)(sizeInBytes % 4) * 8); // trim value
          sourceOffset = snapshot.SnapshotHandleWithoutSize;

          if (sizeInBytes <= BytesOfVariableSnapshotHandleCanStoreInline) // inline content
            return new SnapshotHandle(valueToSet, sizeInBytes);

          break;
        }

        default:
        {
          Assert(elementIndex < myIntsPerSnapshotWithoutHash, "Element index is out of range");

          sourceOffset = snapshot.Handle;
          break;
        }
      }

      var existingValue = myStorage.GetUint32(sourceOffset, elementIndex);
      if (existingValue == valueToSet)
      {
        return snapshot; // the same value
      }

      // todo: 4 byte snapshots

      var currentHash = myStorage.GetUint32(sourceOffset, myIntsPerSnapshotWithoutHash);

      var hashWithoutElement = currentHash ^ HashPart(existingValue, elementIndex);
      var newHash = hashWithoutElement ^ HashPart(valueToSet, elementIndex);

      var withOneValueChanged = new ExistingPoolSnapshotWithOneValueChangedExternalKey(
        this, snapshot, newHash, valueToSet, elementIndex);

      if (myExistingSnapshots.TryGetKey(withOneValueChanged, out var existingHandle))
      {
        return existingHandle;
      }
      
      var newHandle = myStorage.AllocateNewHandle(myIntsPerSnapshotWithHash);

      myStorage.Copy(snapshot, newHandle, myIntsPerSnapshotWithHash);
      myStorage.MutateUint32(newHandle, elementIndex, valueToSet);
      myStorage.MutateUint32(newHandle, myIntsPerSnapshotWithoutHash, newHash);

      myExistingSnapshots.Add(newHandle, new TrivialExternalKey(newHandle, newHash));

      return newHandle;
    }

    private readonly struct TrivialExternalKey : ExternalKeysHashSet<SnapshotHandle>.IExternalKey
    {
      private readonly SnapshotHandle mySnapshot;
      private readonly uint myNewHash;

      public TrivialExternalKey(SnapshotHandle snapshot, uint newHash)
      {
        mySnapshot = snapshot;
        myNewHash = newHash;
      }

      public bool Equals(SnapshotHandle candidateSnapshot) => candidateSnapshot == mySnapshot;

      public uint HashCode() => myNewHash;
    }

    private readonly struct ExistingPoolSnapshotWithOneValueChangedExternalKey : ExternalKeysHashSet<SnapshotHandle>.IExternalKey
    {
      [NotNull] private readonly SnapshotPool<TSnapshotStorage> myPool;
      private readonly SnapshotHandle mySourceSnapshot;
      private readonly uint myNewHash;
      private readonly uint myValueToSet;
      private readonly uint myElementIndex;

      public ExistingPoolSnapshotWithOneValueChangedExternalKey(
        SnapshotPool<TSnapshotStorage> pool, SnapshotHandle sourceSnapshot, uint newHash, uint valueToSet, uint elementIndex)
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

        if (storage.GetUint32(candidateSnapshot.Handle, myElementIndex) != myValueToSet) return false;

        if (!storage.CompareRange(mySourceSnapshot, candidateSnapshot,
                                  startIndex: myElementIndex + 1,
                                  endIndex: myPool.myIntsPerSnapshotWithoutHash)) return false;

        return true;
      }

      public uint HashCode() => myNewHash;
    }

    [MustUseReturnValue]
    public SnapshotHandle AppendBytesToSnapshot(
      SnapshotHandle snapshot, uint bytesToAdd, uint maskToBeAppliedToFirstOfAppendedUint32Elements = 0)
    {
      Assert(myIntsPerSnapshotWithoutHash == VariableSizeSnapshot,
        "Should only be invoked for variable-size snapshot pools");

      var newSizeInBytes = snapshot.SnapshotSizeInBytes + bytesToAdd;
      if (newSizeInBytes <= BytesOfVariableSnapshotHandleCanStoreInline)
      {
        return new SnapshotHandle(
          snapshot.SnapshotHandleWithoutSize | maskToBeAppliedToFirstOfAppendedUint32Elements, newSizeInBytes);
      }
      
      if (newSizeInBytes <= snapshot.SnapshotCapacityInBytes)
      {
        if (maskToBeAppliedToFirstOfAppendedUint32Elements == 0)
        {
          // this works for allocated snapshots, since we guarantee memory in the end to be zeroed
          return new SnapshotHandle(snapshot.SnapshotHandleWithoutSize, newSizeInBytes);
        }

        // todo: test this
        var tailElementIndex = (snapshot.SnapshotSizeInBytes + 1) / 4;
        var lastUint32Element = GetUint32(snapshot, tailElementIndex);
        return SetSingleUInt32(snapshot, tailElementIndex,
          valueToSet: lastUint32Element | maskToBeAppliedToFirstOfAppendedUint32Elements);
      }

      if (newSizeInBytes == 4)
      {
        var newValue = snapshot.SnapshotHandleWithoutSize | maskToBeAppliedToFirstOfAppendedUint32Elements;

        var singleIntExternalKey = new SingleIntExternalKey(this, newValue);
        if (myExistingSnapshots.TryGetKey(singleIntExternalKey, out var existingHandle))
          return existingHandle;

        var newHandle = myStorage.AllocateNewHandle(1);
        myStorage.MutateUint32(newHandle, elementIndex: 0, newValue);

        var newHandleWithSize = new SnapshotHandle(newHandle.Handle, newSizeInBytes);
        myExistingSnapshots.Add(newHandleWithSize, new TrivialExternalKey(newHandleWithSize, newHash: newValue));
        return newHandleWithSize;
      }

      
      

      

      throw null;
    }

    private readonly struct SingleIntExternalKey : ExternalKeysHashSet<SnapshotHandle>.IExternalKey
    {
      [NotNull] private readonly SnapshotPool<TSnapshotStorage> myPool;
      private readonly uint myValue;

      public SingleIntExternalKey([NotNull] SnapshotPool<TSnapshotStorage> pool, uint value)
      {
        myPool = pool;
        myValue = value;
      }
      
      public bool Equals(SnapshotHandle candidateHandle)
      {
        var value = myPool.myStorage.GetUint32(candidateHandle.SnapshotHandleWithoutSize, index: 0);
        return value == myValue;
      }

      public uint HashCode() => myValue; // neat
    }
    
    private void AllocateSharedSnapshot(uint snapshotSize)
    {
      switch (mySharedSnapshotSize)
      {
        case -1:
          mySharedSnapshot = myStorage.AllocateNewHandle(snapshotSize);
          mySharedSnapshotSize = (int) snapshotSize;
          break;

        case int size when size < snapshotSize:
          // todo: allocate more, maybe transfer the data?
          throw new NotImplementedException();
      }
    }
    
    public void LoadToSharedSnapshot(SnapshotHandle snapshot)
    {
      if (myIntsPerSnapshotWithoutHash == 1)
      {
        mySharedSnapshot = snapshot;
        mySharedSnapshotSize = 1;
      }
      else
      {
        AllocateSharedSnapshot(myIntsPerSnapshotWithHash);
        myStorage.Copy(snapshot, mySharedSnapshot, myIntsPerSnapshotWithHash);
      }
    }

    [NotNull, Pure]
    public uint[] SnapshotToDebugArray(SnapshotHandle snapshot)
    {
      uint elementsCount, offset;

      switch (myIntsPerSnapshotWithoutHash)
      {
        case 1:
          return new[] {snapshot.Handle};

        case VariableSizeSnapshot:
        {
          var snapshotSizeInBytes = snapshot.SnapshotSizeInBytes;
          if (snapshotSizeInBytes == 0)
            return new uint[0];

          if (snapshotSizeInBytes <= BytesOfVariableSnapshotHandleCanStoreInline)
            return new[] {snapshot.SnapshotHandleWithoutSize};

          elementsCount = snapshotSizeInBytes / 4;
          offset = snapshot.SnapshotHandleWithoutSize;
          break;
        }

        default:
          elementsCount = myIntsPerSnapshotWithoutHash;
          offset = snapshot.Handle;
          break;
      }

      var array = new uint[elementsCount];
      for (uint index = 0; index < elementsCount; index++)
      {
        array[index] = myStorage.GetUint32(offset, index);
      }

      return array;
    }

    [Pure]
    public uint GetSharedSnapshotUint32(uint elementIndex)
    {
      if (myIntsPerSnapshotWithoutHash == 1)
      {
        return mySharedSnapshot.Handle;
      }

      return myStorage.GetUint32(mySharedSnapshot.Handle, elementIndex);
    }

    public void SetSharedSnapshotUint32(uint elementIndex, uint valueToSet)
    {
      Debug.Assert(elementIndex < mySharedSnapshotSize); // todo: why this do not works?
      
      if (elementIndex >= mySharedSnapshotSize)
        throw new ArgumentOutOfRangeException(nameof(elementIndex));

      if (myIntsPerSnapshotWithoutHash == 1)
      {
        mySharedSnapshot = new SnapshotHandle(valueToSet);
      }
      else
      {
        var existingValue = myStorage.GetUint32(mySharedSnapshot.Handle, elementIndex);
        var sharedHash = myStorage.GetUint32(mySharedSnapshot.Handle, myIntsPerSnapshotWithoutHash);

        var hashWithoutElement = sharedHash ^ HashPart(existingValue, elementIndex);
        var newHash = hashWithoutElement ^ HashPart(valueToSet, elementIndex);

        myStorage.MutateUint32(mySharedSnapshot, elementIndex, valueToSet);
        myStorage.MutateUint32(mySharedSnapshot, myIntsPerSnapshotWithoutHash, newHash);
      }
    }

    [MustUseReturnValue]
    public SnapshotHandle StoreSharedSnapshot()
    {
      if (myIntsPerSnapshotWithoutHash == 1)
      {
        return mySharedSnapshot;
      }

      var sharedArrayExternalKey = new SharedSnapshotExternalKey(this, mySharedSnapshot);

      if (myExistingSnapshots.TryGetKey(sharedArrayExternalKey, out var existingHandle))
      {
        return existingHandle;
      }

      var newHandle = myStorage.AllocateNewHandle(myIntsPerSnapshotWithHash);
      myStorage.Copy(mySharedSnapshot, newHandle, myIntsPerSnapshotWithHash);
      var newHash = myStorage.GetUint32(newHandle.Handle, index: myIntsPerSnapshotWithoutHash);

      myExistingSnapshots.Add(newHandle, new TrivialExternalKey(newHandle, newHash));

      return newHandle;
    }

    private readonly struct SharedSnapshotExternalKey : ExternalKeysHashSet<SnapshotHandle>.IExternalKey
    {
      [NotNull] private readonly SnapshotPool<TSnapshotStorage> myPool;
      private readonly SnapshotHandle mySharedSnapshot;

      public SharedSnapshotExternalKey([NotNull] SnapshotPool<TSnapshotStorage> pool, SnapshotHandle sharedSnapshot)
      {
        myPool = pool;
        mySharedSnapshot = sharedSnapshot;
      }

      public bool Equals(SnapshotHandle candidateSnapshot)
      {
        return myPool.myStorage.CompareRange(
          candidateSnapshot, mySharedSnapshot, 0, myPool.myIntsPerSnapshotWithoutHash);
      }

      public uint HashCode() => myPool.myStorage.GetUint32(mySharedSnapshot.Handle, myPool.myIntsPerSnapshotWithoutHash);
    }
  }
}