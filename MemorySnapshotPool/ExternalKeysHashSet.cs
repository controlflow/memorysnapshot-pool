using System;
using System.Diagnostics;
using JetBrains.Annotations;

namespace MemorySnapshotPool
{
  public struct ExternalKeysHashSet<TKeyHandle>
  {
    private struct Entry
    {
      public int HashCode; // Lower 31 bits of hash code, -1 if unused
      public int Next; // Index of next entry, -1 if last
      public TKeyHandle KeyHandle; // Key handle
    }

    [CanBeNull] private int[] myBuckets;
    [CanBeNull] private Entry[] myEntries;
    private int myCount;
    private int myFreeList;
    private int myFreeCount;

    public ExternalKeysHashSet(int capacity) : this()
    {
      if (capacity > 0)
      {
        Initialize(capacity);
      }
    }

    public int Count
    {
      get { return myCount - myFreeCount; }
    }

    public bool Add<TExteralKey>(TExteralKey externalKey)
      where TExteralKey : struct, IExteralKey
    {
      return Insert(externalKey);
    }

    public void Clear()
    {
      if (myCount > 0)
      {
        var buckets = myBuckets;
        if (buckets != null)
        {
          for (var index = 0; index < buckets.Length; index++)
          {
            buckets[index] = -1;
          }
        }

        if (myEntries != null)
        {
          Array.Clear(myEntries, 0, myCount);
        }

        myFreeList = -1;
        myCount = 0;
        myFreeCount = 0;
      }
    }

    [Pure]
    public bool Contains<TExteralKey>(TExteralKey externalKey)
      where TExteralKey : struct, IExteralKey
    {
      var index = FindEntry(externalKey);
      return index >= 0;
    }

    [Pure]
    private int FindEntry<TExteralKey>(TExteralKey externalKey)
      where TExteralKey : struct, IExteralKey
    {
      if (myBuckets != null)
      {
        var hashCode = externalKey.GetHashCode() & 0x7FFFFFFF;

        for (var index = myBuckets[hashCode % myBuckets.Length]; index >= 0; index = myEntries[index].Next)
        {
          var entry = myEntries[index];
          if (entry.HashCode == hashCode && externalKey.Equals(entry.KeyHandle)) return index;
        }
      }

      return -1;
    }

    private void Initialize(int capacity)
    {
      var size = HashHelpers.GetPrime(capacity);
      myBuckets = new int[size];
      for (var i = 0; i < myBuckets.Length; i++) myBuckets[i] = -1;
      myEntries = new Entry[size];
      myFreeList = -1;
    }

    private bool Insert<TExternalKey>(TExternalKey externalKey)
      where TExternalKey : struct, IExteralKey
    {
      if (myBuckets == null) Initialize(0);

      var buckets = myBuckets;
      var hashCode = externalKey.GetHashCode() & 0x7FFFFFFF;
      var targetBucket = hashCode % buckets.Length;

      for (var index = buckets[targetBucket]; index >= 0; index = myEntries[index].Next)
      {
        if (myEntries[index].HashCode == hashCode && externalKey.Equals(myEntries[index].KeyHandle))
        {
          return false;
        }
      }

      int freeIndex;
      if (myFreeCount > 0)
      {
        freeIndex = myFreeList;
        myFreeList = myEntries[freeIndex].Next;
        myFreeCount--;
      }
      else
      {
        if (myCount == myEntries.Length)
        {
          Resize();
          targetBucket = hashCode % buckets.Length;
        }

        freeIndex = myCount;
        myCount++;
      }

      myEntries[freeIndex].HashCode = hashCode;
      myEntries[freeIndex].Next = buckets[targetBucket];
      myEntries[freeIndex].KeyHandle = externalKey.Handle;
      buckets[targetBucket] = freeIndex;
      return true;
    }

    private void Resize()
    {
      Resize(HashHelpers.ExpandPrime(myCount));
    }

    private void Resize(int newSize)
    {
      Debug.Assert(newSize >= myEntries.Length);

      var newBuckets = new int[newSize];
      for (var index = 0; index < newBuckets.Length; index++)
      {
        newBuckets[index] = -1;
      }

      var newEntries = new Entry[newSize];
      Array.Copy(myEntries, 0, newEntries, 0, myCount);

      for (var index = 0; index < myCount; index++)
      {
        if (newEntries[index].HashCode >= 0)
        {
          var bucket = newEntries[index].HashCode % newSize;
          newEntries[index].Next = newBuckets[bucket];
          newBuckets[bucket] = index;
        }
      }

      myBuckets = newBuckets;
      myEntries = newEntries;
    }

    [Pure, ContractAnnotation("=> false, keyHandle: null; => true")]
    public bool TryGetKey<TExternalKey>(TExternalKey externalKey, out TKeyHandle keyHandle)
      where TExternalKey : struct, IExteralKey
    {
      var index = FindEntry(externalKey);
      if (index >= 0)
      {
        keyHandle = myEntries[index].KeyHandle;
        return true;
      }

      keyHandle = default(TKeyHandle);
      return false;
    }

    public interface IExteralKey
    {
      TKeyHandle Handle { get; }
      [Pure] bool Equals(TKeyHandle keyHandle);
      [Pure] int GetHashCode();
    }
  }
}