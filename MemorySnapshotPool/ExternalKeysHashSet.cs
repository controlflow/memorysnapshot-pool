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

    private int[] myBuckets;
    private Entry[] myEntries;
    private int myCount;
    private int myCollisions;
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

    public int Collisions
    {
      get { return myCollisions; }
    }

    public bool Add<TExteralKey>(TKeyHandle keyHandle, TExteralKey externalKey)
      where TExteralKey : struct, IExteralKey
    {
      return Insert(keyHandle, externalKey);
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
        var hashCode = externalKey.HashCode() & 0x7FFFFFFF;

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

      for (var index = 0; index < myBuckets.Length; index++)
      {
        myBuckets[index] = -1;
      }

      myEntries = new Entry[size];
      myFreeList = -1;
    }

    private bool Insert<TExternalKey>(TKeyHandle keyHandle, TExternalKey externalKey)
      where TExternalKey : struct, IExteralKey
    {
      if (myBuckets == null) Initialize(0);

      var hashCode = (int) externalKey.HashCode() & 0x7FFFFFFF;
      var targetBucket = hashCode % myBuckets.Length;
      var hashCollision = false;

      for (var index = myBuckets[targetBucket]; index >= 0; index = myEntries[index].Next)
      {
        if (myEntries[index].HashCode != hashCode) continue;

        hashCollision = true;

        if (externalKey.Equals(myEntries[index].KeyHandle)) return false;
      }

      if (hashCollision) myCollisions++;

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
          targetBucket = hashCode % myBuckets.Length;
        }

        freeIndex = myCount;
        myCount++;
      }

      myEntries[freeIndex].HashCode = hashCode;
      myEntries[freeIndex].Next = myBuckets[targetBucket];
      myEntries[freeIndex].KeyHandle = keyHandle;
      myBuckets[targetBucket] = freeIndex;
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

    [Pure]
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
      [Pure] bool Equals(TKeyHandle candidateHandle);
      [Pure] uint HashCode();
    }
  }

  internal static class HashHelpers
  {
    private static readonly int[] ourPrimes =
    {
      3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
      1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
      17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
      187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
      1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369
    };

    [Pure]
    public static bool IsPrime(int candidate)
    {
      if ((candidate & 1) != 0)
      {
        var limit = (int)Math.Sqrt(candidate);
        for (var divisor = 3; divisor <= limit; divisor += 2)
        {
          if (candidate % divisor == 0)
            return false;
        }

        return true;
      }

      return candidate == 2;
    }

    private const int HashPrime = 101;

    public static int GetPrime(int min)
    {
      if (min < 0)
        throw new ArgumentException();

      foreach (var prime in ourPrimes)
      {
        if (prime >= min) return prime;
      }

      for (var i = (min | 1); i < int.MaxValue; i += 2)
      {
        if (IsPrime(i) && (i - 1) % HashPrime != 0) return i;
      }

      return min;
    }

    public static int ExpandPrime(int oldSize)
    {
      var newSize = 2 * oldSize;

      if ((uint)newSize > MaxPrimeArrayLength && MaxPrimeArrayLength > oldSize)
      {
        Debug.Assert(MaxPrimeArrayLength == GetPrime(MaxPrimeArrayLength));
        return MaxPrimeArrayLength;
      }

      return GetPrime(newSize);
    }

    private const int MaxPrimeArrayLength = 0x7FEFFFFD;
  }
}