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

    // todo: can inline this into pool array, as the begginning
    private readonly byte[] mySharedSnapshotArray;
    private int mySharedSnapshotHash;

    private int myLastUsedHandle;

    // todo: can store inline in array
    private readonly Dictionary<SnapshotHandle, int> myHandleToHash;

    // todo: can replace with inlined hashtable impl
    //private readonly MultiValueDictionary<int, SnapshotHandle> myExistingSnapshots;
    private ExternalKeysHashSet<SnapshotHandle> myExistingSnapshots;

    public SnapshotPool(int bytesPerSnapshot)
    {
      Debug.Assert(bytesPerSnapshot >= 0);


      myPoolArray = new byte[bytesPerSnapshot * 100];
      mySharedSnapshotArray = new byte[bytesPerSnapshot];
      myBytesPerSnapshot = bytesPerSnapshot;

      myLastUsedHandle = 1;
      myHandleToHash = new Dictionary<SnapshotHandle, int> {{ZeroSnapshot, 0}};
      myExistingSnapshots = new ExternalKeysHashSet<SnapshotHandle>();
      myExistingSnapshots.Add(new ZeroSnapshotExternalKey());
    }

    private struct ZeroSnapshotExternalKey : ExternalKeysHashSet<SnapshotHandle>.IExteralKey
    {
      public SnapshotHandle Handle { get { return new SnapshotHandle(0); } }

      public bool Equals(SnapshotHandle keyHandle)
      {
        throw new InvalidOperationException();
      }

      public override int GetHashCode() { return 0; }
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

      SnapshotHandle existingHandle;
      var withOneValueChanged = new PoolExternalKeyWithOneValueChanged(this, newHash, sourceArray, sourceShift, valueToSet, elementIndex);
      if (myExistingSnapshots.TryGetKey(withOneValueChanged, out existingHandle))
      {
        return existingHandle;
      }

      var newExternalKey = withOneValueChanged.AllocateChanged();
      myExistingSnapshots.Add(newExternalKey);

      return newExternalKey.Handle;
    }

    private struct ExistingPoolExternalKey : ExternalKeysHashSet<SnapshotHandle>.IExteralKey
    {
      [NotNull] private readonly SnapshotPool myPool;
      private readonly SnapshotHandle myHandle;

      public ExistingPoolExternalKey([NotNull] SnapshotPool pool, SnapshotHandle handle)
      {
        myHandle = handle;
        myPool = pool;
      }

      public SnapshotHandle Handle { get { return myHandle; } }

      public bool Equals(SnapshotHandle keyHandle)
      {
        return keyHandle == myHandle;
      }

      public override int GetHashCode()
      {
        // todo: store inline in array
        return myPool.myHandleToHash[myHandle];
      }
    }

    private struct PoolExternalKeyWithOneValueChanged : ExternalKeysHashSet<SnapshotHandle>.IExteralKey
    {
      [NotNull] private readonly SnapshotPool myPool;
      private readonly int myNewHash;
      private readonly byte[] mySourceArray;
      private readonly int mySourceShift;
      private readonly byte myValueToSet;
      private readonly int myElementIndex;

      public PoolExternalKeyWithOneValueChanged(SnapshotPool pool, int newHash, byte[] sourceArray, int sourceShift, byte valueToSet, int elementIndex)
      {
        myPool = pool;
        myNewHash = newHash;
        mySourceArray = sourceArray;
        mySourceShift = sourceShift;
        myValueToSet = valueToSet;
        myElementIndex = elementIndex;
      }

      public SnapshotHandle Handle
      {
        get { throw new InvalidOperationException("Not yet has a handle"); }
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

        for (var index = myElementIndex + 1; index < myPool.myBytesPerSnapshot; index++)
        {
          if (mySourceArray[mySourceShift + index] != candidateArray[candidateShift + index]) return false;
        }

        return true;
      }

      public override int GetHashCode()
      {
        return myNewHash;
      }

      [MustUseReturnValue]
      public ExistingPoolExternalKey AllocateChanged()
      {
        var newHandle = myPool.AllocNewHandle();

        int newShift;
        var newArray = myPool.GetArray(newHandle, out newShift);
        Array.Copy(
          sourceArray: mySourceArray,
          sourceIndex: mySourceShift,
          destinationArray: newArray,
          destinationIndex: newShift,
          length: myPool.myBytesPerSnapshot);

        newArray[newShift + myElementIndex] = myValueToSet;

        // todo: store inline
        myPool.myHandleToHash.Add(newHandle, myNewHash);

        return new ExistingPoolExternalKey(myPool, newHandle);
      }
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

    [NotNull, MustUseReturnValue]
    public byte[] ReadToSharedSnapshotArray(SnapshotHandle snapshot)
    {
      int sourceShift;
      var sourceArray = GetArray(snapshot, out sourceShift);

      Array.Copy(
        sourceArray: sourceArray,
        sourceIndex: sourceShift,
        destinationArray: mySharedSnapshotArray,
        destinationIndex: 0,
        length: myBytesPerSnapshot);

      mySharedSnapshotHash = myHandleToHash[snapshot];

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

      var newExternalKey = sharedArrayExternalKey.AllocateChanged();
      myExistingSnapshots.Add(newExternalKey);

      return newExternalKey.Handle;
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

      public SnapshotHandle Handle
      {
        get { throw new InvalidOperationException("Not yet has a handle"); }
      }

      public bool Equals(SnapshotHandle keyHandle)
      {
        int candidateShift;
        var candidateArray = myPool.GetArray(keyHandle, out candidateShift);

        for (var index = 0; index < myPool.myBytesPerSnapshot; index++)
        {
          if (myPool.mySharedSnapshotArray[index] != candidateArray[candidateShift + index]) return false;
        }

        return true;
      }

      public override int GetHashCode()
      {
        return mySharedSnapshotHash;
      }

      [MustUseReturnValue]
      public ExistingPoolExternalKey AllocateChanged()
      {
        var newHandle = myPool.AllocNewHandle();

        int newShift;
        var newArray = myPool.GetArray(newHandle, out newShift);
        Array.Copy(
          sourceArray: myPool.mySharedSnapshotArray,
          sourceIndex: 0,
          destinationArray: newArray,
          destinationIndex: newShift,
          length: myPool.myBytesPerSnapshot);

        // todo: store inline
        myPool.myHandleToHash.Add(newHandle, mySharedSnapshotHash);

        return new ExistingPoolExternalKey(myPool, newHandle);
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

      var newExternalKey = sharedArrayExternalKey.AllocateChanged();
      myExistingSnapshots.Add(newExternalKey);

      return newExternalKey.Handle;
    }
  }

  //public class ByteSnapshotPool : SnapshotPool
  //{
    
  //}

  //public class Int32SnapshotPool : SnapshotPool
  //{
    
  //}
}