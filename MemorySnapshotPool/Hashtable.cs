using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;

public interface IExteralComparer<in T>
{
  bool Equals(T value);
}

public class ExternalKeysHashtable<T>
{
  private struct Entry
  {
    public int HashCode; // Lower 31 bits of hash code, -1 if unused
    public int Next; // Index of next entry, -1 if last
    public T Value; // Value of entry
  }

  private int[] myBuckets;
  private Entry[] myEntries;
  private int myCount;
  private int myFreeList;
  private int myFreeCount;

  public ExternalKeysHashtable(int capacity)
  {
    if (capacity > 0) Initialize(capacity);
  }

  public int Count
  {
    get { return myCount - myFreeCount; }
  }

  public void Add(int keyHashCode, IExteralComparer<T> exteralComparer, T value)
  {
    Insert(keyHashCode, exteralComparer, value, add: true);
  }

  public void Clear()
  {
    if (myCount > 0)
    {
      for (var i = 0; i < myBuckets.Length; i++) myBuckets[i] = -1;
      Array.Clear(myEntries, 0, myCount);
      myFreeList = -1;
      myCount = 0;
      myFreeCount = 0;
    }
  }

  public bool ContainsKey(int keyHashCode, IExteralComparer<T> exteralComparer)
  {
    return FindEntry(keyHashCode, exteralComparer) >= 0;
  }

  private int FindEntry(int keyHashCode, IExteralComparer<T> exteralComparer)
  {
    if (myBuckets != null)
    {
      var hashCode = keyHashCode & 0x7FFFFFFF;
      for (var i = myBuckets[hashCode % myBuckets.Length]; i >= 0; i = myEntries[i].Next)
      {
        if (myEntries[i].HashCode == hashCode)
        {
          if (exteralComparer.Equals(myEntries[i].Value))
          {
            return i;
          }
        }
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

  private void Insert(int keyHashCode, IExteralComparer<T> exteralComparer, T value, bool add)
  {
    if (myBuckets == null) Initialize(0);

    var hashCode = keyHashCode & 0x7FFFFFFF;
    var targetBucket = hashCode % myBuckets.Length;

    for (var i = myBuckets[targetBucket]; i >= 0; i = myEntries[i].Next)
    {
      if (myEntries[i].HashCode == hashCode && exteralComparer.Equals(myEntries[i].Value))
      {
        if (add)
        {
          Debug.Fail("Duplicate");
        }

        myEntries[i].Value = value;
        return;
      }
    }

    int index;
    if (myFreeCount > 0)
    {
      index = myFreeList;
      myFreeList = myEntries[index].Next;
      myFreeCount--;
    }
    else
    {
      if (myCount == myEntries.Length)
      {
        Resize();
        targetBucket = hashCode % myBuckets.Length;
      }

      index = myCount;
      myCount++;
    }

    myEntries[index].HashCode = hashCode;
    myEntries[index].Next = myBuckets[targetBucket];
    myEntries[index].Value = value;
    myBuckets[targetBucket] = index;
  }

  private void Resize()
  {
    Resize(HashHelpers.ExpandPrime(myCount));
  }

  private void Resize(int newSize)
  {
    Debug.Assert(newSize >= myEntries.Length);

    var newBuckets = new int[newSize];
    for (var i = 0; i < newBuckets.Length; i++) newBuckets[i] = -1;

    var newEntries = new Entry[newSize];
    Array.Copy(myEntries, 0, newEntries, 0, myCount);

    for (var i = 0; i < myCount; i++)
    {
      if (newEntries[i].HashCode >= 0)
      {
        var bucket = newEntries[i].HashCode % newSize;
        newEntries[i].Next = newBuckets[bucket];
        newBuckets[bucket] = i;
      }
    }

    myBuckets = newBuckets;
    myEntries = newEntries;
  }

  public bool TryGetValue(int keyHashCode, IExteralComparer<T> exteralComparer, out T value)
  {
    var i = FindEntry(keyHashCode, exteralComparer);
    if (i >= 0)
    {
      value = myEntries[i].Value;
      return true;
    }

    value = default(T);
    return false;
  }
}



internal static class HashHelpers
{
  // Table of prime numbers to use as hash table sizes. 
  // A typical resize algorithm would pick the smallest prime number in this array
  // that is larger than twice the previous capacity. 
  // Suppose our Hashtable currently has capacity x and enough elements are added 
  // such that a resize needs to occur. Resizing first computes 2x then finds the 
  // first prime in the table greater than 2x, i.e. if primes are ordered 
  // p_1, p_2, ..., p_i, ..., it finds p_n such that p_n-1 < 2x < p_n. 
  // Doubling is important for preserving the asymptotic complexity of the 
  // hashtable operations such as add.  Having a prime guarantees that double 
  // hashing does not lead to infinite loops.  IE, your hash function will be 
  // h1(key) + i*h2(key), 0 <= i < size.  h2 and the size must be relatively prime.
  public static readonly int[] primes =
  {
    3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
    1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
    17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
    187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
    1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369
  };

  public static bool IsPrime(int candidate)
  {
    if ((candidate & 1) != 0)
    {
      var limit = (int) Math.Sqrt(candidate);
      for (var divisor = 3; divisor <= limit; divisor += 2)
      {
        if ((candidate % divisor) == 0)
          return false;
      }

      return true;
    }

    return (candidate == 2);
  }

  internal const int HashPrime = 101;

  public static int GetPrime(int min)
  {
    if (min < 0)
      throw new ArgumentException();

    foreach (var prime in primes)
    {
      if (prime >= min) return prime;
    }

    //outside of our predefined table. 
    //compute the hard way. 
    for (var i = (min | 1); i < Int32.MaxValue; i += 2)
    {
      if (IsPrime(i) && ((i - 1) % HashPrime != 0))
        return i;
    }

    return min;
  }

  public static int GetMinPrime()
  {
    return primes[0];
  }

  // Returns size of hashtable to grow to.
  public static int ExpandPrime(int oldSize)
  {
    var newSize = 2 * oldSize;

    // Allow the hashtables to grow to maximum possible size (~2G elements) before encoutering capacity overflow.
    // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
    if ((uint) newSize > MaxPrimeArrayLength && MaxPrimeArrayLength > oldSize)
    {
      Contract.Assert(MaxPrimeArrayLength == GetPrime(MaxPrimeArrayLength), "Invalid MaxPrimeArrayLength");
      return MaxPrimeArrayLength;
    }

    return GetPrime(newSize);
  }


  // This is the maximum prime smaller than Array.MaxArrayLength
  public const int MaxPrimeArrayLength = 0x7FEFFFFD;
}