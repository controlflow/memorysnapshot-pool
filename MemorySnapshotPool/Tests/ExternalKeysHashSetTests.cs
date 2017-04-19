using System.Collections.Generic;
using JetBrains.Annotations;
using NUnit.Framework;

namespace MemorySnapshotPool.Tests
{
  [TestFixture]
  public class ExternalKeysHashSetTests
  {
    [Test]
    public void BasicTests()
    {
      var array = new[] {"abc", "def"};

      BasicTestScenario(
        firstKey: new ArrayElementExternalKey<string>(array, index: 0),
        secondKey: new ArrayElementExternalKey<string>(array, index: 1));
    }

    private static void BasicTestScenario<TExternalKey>(TExternalKey firstKey, TExternalKey secondKey)
      where TExternalKey : struct, ExternalKeysHashSet<int>.IExteralKey
    {
      var hashSet = new ExternalKeysHashSet<int>(capacity: 0);
      Assert.AreEqual(0, hashSet.Count);

      Assert.IsTrue(hashSet.Add(0, firstKey));
      Assert.AreEqual(1, hashSet.Count);

      {
        Assert.IsTrue(hashSet.Contains(firstKey));

        int existingHandle;
        Assert.IsTrue(hashSet.TryGetKey(firstKey, out existingHandle));
        Assert.AreEqual(0, existingHandle);
      }

      hashSet.Add(0, firstKey);

      Assert.IsFalse(hashSet.Add(0, firstKey));
      Assert.AreEqual(1, hashSet.Count);

      Assert.IsTrue(hashSet.Add(1, secondKey));
      Assert.AreEqual(2, hashSet.Count);

      {
        Assert.IsTrue(hashSet.Contains(firstKey));
        Assert.IsTrue(hashSet.Contains(secondKey));

        int existingHandle;
        Assert.IsTrue(hashSet.TryGetKey(firstKey, out existingHandle));
        Assert.AreEqual(0, existingHandle);

        Assert.IsTrue(hashSet.TryGetKey(secondKey, out existingHandle));
        Assert.AreEqual(1, existingHandle);
      }

      hashSet.Clear();
      Assert.AreEqual(0, hashSet.Count);
    }

    private struct ArrayElementExternalKey<T> : ExternalKeysHashSet<int>.IExteralKey
    {
      [NotNull] private readonly T[] myArray;
      private readonly int myIndex;

      public ArrayElementExternalKey([NotNull] T[] array, int index)
      {
        myArray = array;
        myIndex = index;
      }

      public bool Equals(int keyHandle)
      {
        return EqualityComparer<T>.Default.Equals(myArray[keyHandle], myArray[myIndex]);
      }

      public int HashCode()
      {
        return myArray[myIndex].GetHashCode();
      }
    }

    [Test]
    public void HashCollision()
    {
      var array = new[] { "abc", "def" };

      BasicTestScenario(
        firstKey: new CollidingHashArrayElementExternalKey<string>(array, index: 0),
        secondKey: new CollidingHashArrayElementExternalKey<string>(array, index: 1));
    }

    private struct CollidingHashArrayElementExternalKey<T> : ExternalKeysHashSet<int>.IExteralKey
    {
      [NotNull] private readonly T[] myArray;
      private readonly int myIndex;

      public CollidingHashArrayElementExternalKey([NotNull] T[] array, int index)
      {
        myArray = array;
        myIndex = index;
      }

      public bool Equals(int keyHandle)
      {
        return EqualityComparer<T>.Default.Equals(myArray[keyHandle], myArray[myIndex]);
      }

      public int HashCode() { return 42; }
    }
  }
}