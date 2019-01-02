using System.Collections.Generic;
using System.Linq;
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
      where TExternalKey : struct, ExternalKeysHashSet<int>.IExternalKey
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

    private struct ArrayElementExternalKey<T> : ExternalKeysHashSet<int>.IExternalKey
    {
      [NotNull] private readonly T[] myArray;
      private readonly int myIndex;

      public ArrayElementExternalKey([NotNull] T[] array, int index)
      {
        myArray = array;
        myIndex = index;
      }

      public bool Equals(int candidateHandle)
      {
        return EqualityComparer<T>.Default.Equals(myArray[candidateHandle], myArray[myIndex]);
      }

      public uint HashCode()
      {
        return (uint) myArray[myIndex].GetHashCode();
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

    private struct CollidingHashArrayElementExternalKey<T> : ExternalKeysHashSet<int>.IExternalKey
    {
      [NotNull] private readonly T[] myArray;
      private readonly int myIndex;

      public CollidingHashArrayElementExternalKey([NotNull] T[] array, int index)
      {
        myArray = array;
        myIndex = index;
      }

      public bool Equals(int candidateHandle)
      {
        return EqualityComparer<T>.Default.Equals(myArray[candidateHandle], myArray[myIndex]);
      }

      public uint HashCode() { return 42; }
    }

    [Test]
    public void Resize()
    {
      var chars = Enumerable.Range('a', 'z' - 'a').Select(i => (char) i).ToList();
      var permutations =
        from a in chars
        from b in chars
        from c in chars
        select a.ToString() + b + c;

      var array = permutations.ToArray();
      var hashSet = new ExternalKeysHashSet<int>(capacity: 0);

      for (var handle = 0; handle < array.Length; handle++)
      {
        hashSet.Add(handle, new ArrayElementExternalKey<string>(array, handle));
      }

      Assert.That(hashSet.Count, Is.EqualTo(array.Length));

      for (var handle = 0; handle < array.Length; handle++)
      {
        Assert.That(hashSet.Contains(new ArrayElementExternalKey<string>(array, handle)));
      }
    }
  }
}