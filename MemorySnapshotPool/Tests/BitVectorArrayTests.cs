using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NUnit.Framework;

namespace MemorySnapshotPool.Tests
{
  [TestFixture]
  public class BitVectorArrayTest
  {
    private const uint N = 40;
    private const uint BITS_PER_ITEM = 38;

    private BitVectorArray myVectorArray;

    [Test]
    public void TestSetBitForFirstBit()
    {
      var snapshotPool = new BitVectorArraySnapshotPool(N, BITS_PER_ITEM);

      var zeroSnapshot = SnapshotPool.ZeroSnapshot;
      var firstBitSetSnapshot = snapshotPool.SetBit(zeroSnapshot, 0, 0);

      AssertAllBitsNotSet(snapshotPool, zeroSnapshot);
      Assert.True(firstBitSetSnapshot != zeroSnapshot);
      AssertBitsSet(snapshotPool, firstBitSetSnapshot, new[] { 0u, 0u });
    }

    [Test]
    public void TestSetBitTwice()
    {
      var snapshotPool = new BitVectorArraySnapshotPool(N, BITS_PER_ITEM);

      var zeroSnapshot = SnapshotPool.ZeroSnapshot;
      var firstBitSetSnapshot = snapshotPool.SetBit(zeroSnapshot, 0, 0);
      Assert.True(firstBitSetSnapshot != zeroSnapshot);

      var firstBitSetSnapshot2 = snapshotPool.SetBit(zeroSnapshot, 0, 0);
      Assert.True(firstBitSetSnapshot2 != zeroSnapshot);
      Assert.True(firstBitSetSnapshot == firstBitSetSnapshot2);

      AssertBitsSet(snapshotPool, firstBitSetSnapshot, new[] { 0u, 0u });
    }

    [Test]
    public void TestSetBitForMiddleBit()
    {
      var snapshotPool = new BitVectorArraySnapshotPool(N, BITS_PER_ITEM);

      var zeroSnapshot = SnapshotPool.ZeroSnapshot;
      var thirdBitSetSnapshot = snapshotPool.SetBit(zeroSnapshot, 3, 3);

      Assert.True(thirdBitSetSnapshot != zeroSnapshot);
      AssertBitsSet(snapshotPool, thirdBitSetSnapshot, new[] { 3u, 3u });
    }

    [Test]
    public void TestSetBitAndClearOtherBitsForOneItem()
    {
      var snapshotPool = new BitVectorArraySnapshotPool(N, BITS_PER_ITEM);
      var setBits = new[] { new[] { 0u, 0u }, new[] { 1u, 4u } };

      var initialSnapshot = GivenBitVectorArrayWithSetBits(snapshotPool, setBits);

      //snapshotPool.SetSharedSnapshotBit(0, 5);


      BitVectorArray result;
      var opResult = myVectorArray.SetBitAndClearOtherBits(0, 5, out result);

      Assert.True(opResult);
      //AssertBitsSet(myVectorArray, setBits);
      AssertBitsSet(result, new[] { 0, 5 }, new[] { 1, 4 });
    }

    [Test]
    public void TestSetBitAndClearOtherBitsForOneItem2()
    {
      var setBits = new[] { new[] { 5, 10 } };
      GivenBitVectorArrayWithSetBits(setBits);

      BitVectorArray result;
      var opResult = myVectorArray.SetBitAndClearOtherBits(5, 37, out result);

      Assert.True(opResult);
      AssertBitsSet(myVectorArray, setBits);
      AssertBitsSet(result, new[] { 5, 37 });
    }

    [Test]
    public void TestSetBitAndClearOtherBitsForOneItem3()
    {
      var setBits = new[] { new[] { 5, 37 } };
      GivenBitVectorArrayWithSetBits(setBits);

      BitVectorArray result;
      var opResult = myVectorArray.SetBitAndClearOtherBits(5, 10, out result);

      Assert.True(opResult);
      AssertBitsSet(myVectorArray, setBits);
      AssertBitsSet(result, new[] { 5, 10 });
    }

    [Test]
    public void TestSetBitAndClearOtherBitsForSeveralItems()
    {
      var setBits = new[] { new[] { 0, 3 }, new[] { 1, 3 }, new[] { 2, 3 } };
      GivenBitVectorArrayWithSetBits(setBits);

      BitVectorArray result;
      var opResult = myVectorArray.SetBitAndClearOtherBits(new[] { 0, 5 }, 7, out result);

      Assert.True(opResult);
      AssertBitsSet(myVectorArray, setBits);
      AssertBitsSet(result, new[] { 0, 7 }, new[] { 1, 3 }, new[] { 2, 3 }, new[] { 5, 7 });
    }

    [Test]
    public void TestClearSimple()
    {
      var snapshotPool = new BitVectorArraySnapshotPool(arrayLength: 16, bitsPerItem: 2);
      var initialSnapshot = snapshotPool.SetBit(SnapshotPool.ZeroSnapshot, 3, 1);

      Assert.IsFalse(snapshotPool.GetBit(initialSnapshot, 2, 0));
      Assert.IsFalse(snapshotPool.GetBit(initialSnapshot, 2, 1));
      Assert.IsFalse(snapshotPool.GetBit(initialSnapshot, 3, 0));
      Assert.IsTrue(snapshotPool.GetBit(initialSnapshot, 3, 1));
      Assert.IsFalse(snapshotPool.GetBit(initialSnapshot, 4, 0));
      Assert.IsFalse(snapshotPool.GetBit(initialSnapshot, 4, 1));
      Assert.That(snapshotPool.Clear(initialSnapshot, 0) == initialSnapshot);
      Assert.That(snapshotPool.Clear(initialSnapshot, 1) == initialSnapshot);
      Assert.That(snapshotPool.Clear(initialSnapshot, 2) == initialSnapshot);
      Assert.That(snapshotPool.Clear(initialSnapshot, 4) == initialSnapshot);

      var clearedSnapshot = snapshotPool.Clear(initialSnapshot, 3);
      Assert.That(clearedSnapshot != initialSnapshot);
      AssertAllBitsNotSet(snapshotPool, clearedSnapshot);
    }

    [Test]
    public void TestClearForOneItem()
    {
      var snapshotPool = new BitVectorArraySnapshotPool(N, BITS_PER_ITEM);
      var setBits = new[] { new[] { 0u, BITS_PER_ITEM - 1 }, new[] { 1u, 0u }, new[] { 1u, 1u }, new[] { 1u, 7u }, new[] { 2u, 0u }, new[] { 7u, 7u } };
      var initialSnapshot = GivenBitVectorArrayWithSetBits(snapshotPool, setBits);

      var clearedSnapshot = snapshotPool.Clear(initialSnapshot, 1);
      Assert.That(clearedSnapshot != initialSnapshot);

      AssertBitsSet(snapshotPool, initialSnapshot, setBits);
      AssertBitsSet(snapshotPool, clearedSnapshot, new[] { 0u, BITS_PER_ITEM - 1 }, new[] { 2u, 0u }, new[] { 7u, 7u });
    }

    [Test]
    public void TestClearForSeveralItems()
    {
      var snapshotPool = new BitVectorArraySnapshotPool(N, BITS_PER_ITEM);
      var setBits = new[] { new[] { 0u, BITS_PER_ITEM - 1 }, new[] { 1u, 0u }, new[] { 2u, 1u }, new[] { 3u, 0u }, new[] { 4u, 3u }, new[] { 7u, 7u } };
      var initialSnapshot = GivenBitVectorArrayWithSetBits(snapshotPool, setBits);

      var clearedSnapshot = snapshotPool.Clear(initialSnapshot, new[] {1u, 2u, 4u, 10u});

      Assert.That(clearedSnapshot != initialSnapshot);
      AssertBitsSet(snapshotPool, initialSnapshot, setBits);
      AssertBitsSet(snapshotPool, clearedSnapshot, new[] { 0u, BITS_PER_ITEM - 1 }, new[] { 3u, 0u }, new[] { 7u, 7u });
    }

    [Test]
    public void TestClearForUnsetItems()
    {
      var snapshotPool = new BitVectorArraySnapshotPool(N, BITS_PER_ITEM);
      var setBits = new[] { new[] { 0u, BITS_PER_ITEM - 1 }, new[] { 1u, 0u }, new[] { 2u, 1u }, new[] { 3u, 0u }, new[] { 4u, 3u }, new[] { 7u, 7u } };
      var initialSnapshot = GivenBitVectorArrayWithSetBits(snapshotPool, setBits);

      var clearedSnapshot = snapshotPool.Clear(initialSnapshot, new[] {10u, 20u});

      Assert.That(clearedSnapshot == initialSnapshot);
      AssertBitsSet(snapshotPool, clearedSnapshot, setBits);
    }

    /*
    [Test]
    public void TestCopy()
    {
      var setBits = new[] { new[] { 1, 2 }, new[] { 1, BITS_PER_ITEM - 1 }, new[] { 2, 7 } };
      GivenBitVectorArrayWithSetBits(setBits);

      BitVectorArray result;
      var opResult = myVectorArray.Copy(1, 2, out result);

      Assert.True(opResult);
      AssertBitsSet(myVectorArray, setBits);
      AssertBitsSet(result, new[] { 1, 2 }, new[] { 1, BITS_PER_ITEM - 1 }, new[] { 2, 2 }, new[] { 2, BITS_PER_ITEM - 1 });
    }
*/

    [Test]
    public void TestCopy2()
    {
      var setBits = new int[BITS_PER_ITEM * 2][];
      for (var i = 0; i < BITS_PER_ITEM; i++)
      {
        setBits[2 * i] = new[] { 3, i };
        setBits[2 * i + 1] = new[] { 25, i };
      }

      GivenBitVectorArrayWithSetBits(setBits);

      BitVectorArray result;
      var opResult = myVectorArray.Copy(3, 25, out result);

      Assert.False(opResult);
      AssertBitsSet(myVectorArray, setBits);
      Assert.True(myVectorArray == result);
    }

    [Test]
    public void TestCopy3()
    {
      var setBits = new int[BITS_PER_ITEM * 2][];
      for (var i = 0; i < BITS_PER_ITEM; i++)
      {
        setBits[2 * i] = new[] { 3, i };
        setBits[2 * i + 1] = new[] { 25, i };
      }
      setBits[BITS_PER_ITEM * 2 - 1] = new[] { 26, 0 };

      GivenBitVectorArrayWithSetBits(setBits);

      BitVectorArray result;
      var opResult = myVectorArray.Copy(3, 25, out result);

      Assert.True(opResult);
      AssertBitsSet(myVectorArray, setBits);

      var expectedSetBits = new int[BITS_PER_ITEM * 2 + 1][];
      for (var i = 0; i < BITS_PER_ITEM; i++)
      {
        expectedSetBits[2 * i] = new[] { 3, i };
        expectedSetBits[2 * i + 1] = new[] { 25, i };
      }
      expectedSetBits[2 * BITS_PER_ITEM] = new[] { 26, 0 };
      AssertBitsSet(result, expectedSetBits);
    }

    private void GivenBitVectorArrayWithSetBits([NotNull] params int[][] setBits)
    {
      var result = new BitVectorArray((short) N, (short) BITS_PER_ITEM);
      foreach (var position in setBits)
      {
        var b = result.SetBit(position[0], position[1], out result);
        Assert.True(b, "{0} {1}", position[0], position[1]);
        Assert.True(result.GetBit(position[0], position[1]));
      }

      myVectorArray = result;
    }

    [Pure]
    private SnapshotHandle GivenBitVectorArrayWithSetBits([NotNull] BitVectorArraySnapshotPool snapshotPool, [NotNull] params uint[][] setBits)
    {
      snapshotPool.LoadToSharedSnapshot(SnapshotPool.ZeroSnapshot);

      foreach (var position in setBits)
      {
        snapshotPool.SetSharedSnapshotBit(position[0], position[1]);

        Assert.True(snapshotPool.GetBit(SnapshotPool.SharedSnapshot, position[0], position[1]));
      }

      return snapshotPool.StoreSharedSnapshot();
    }

    [AssertionMethod]
    private static void AssertBitsSet(BitVectorArray bitVectorArray, [NotNull] params int[][] setBits)
    {
      AssertBitsNotSet(bitVectorArray, setBits);
    }

    [AssertionMethod]
    private static void AssertAllBitsNotSet(BitVectorArray bitVectorArray)
    {
      AssertBitsNotSet(bitVectorArray);
    }

    [AssertionMethod]
    private static void AssertBitsNotSet(BitVectorArray bitVectorArray, [NotNull] params int[][] exceptPositions)
    {
      var set = new HashSet<Tuple<int, int>>(exceptPositions.Select(ints => new Tuple<int, int>(ints[0], ints[1])));
      for (var i = 0; i < N; i++)
      {
        for (var j = 0; j < BITS_PER_ITEM; j++)
        {
          if (set.Contains(new Tuple<int, int>(i, j)))
            Assert.True(bitVectorArray.GetBit(i, j), "bit ({0}, {1}) is expected to be set", i, j);
          else
            Assert.False(bitVectorArray.GetBit(i, j), "bit ({0}, {1}) is expected to be unset", i, j);
        }
      }
    }

    [AssertionMethod]
    private static void AssertBitsSet([NotNull] BitVectorArraySnapshotPool pool, SnapshotHandle snaphot, [NotNull] params uint[][] setBits)
    {
      AssertBitsNotSet(pool, snaphot, setBits);
    }

    [AssertionMethod]
    private static void AssertAllBitsNotSet([NotNull] BitVectorArraySnapshotPool pool, SnapshotHandle snaphot)
    {
      AssertBitsNotSet(pool, snaphot);
    }

    [AssertionMethod]
    private static void AssertBitsNotSet(
      [NotNull] BitVectorArraySnapshotPool pool, SnapshotHandle snaphot, [NotNull] params uint[][] exceptPositions)
    {
      var set = new HashSet<Tuple<uint, uint>>(exceptPositions.Select(ints => new Tuple<uint, uint>(ints[0], ints[1])));
      for (var i = 0u; i < pool.ArrayLength; i++)
      {
        for (var j = 0u; j < pool.BitsPerItem; j++)
        {
          if (set.Contains(new Tuple<uint, uint>(i, j)))
            Assert.True(pool.GetBit(snaphot, i, j), "bit ({0}, {1}) is expected to be set", i, j);
          else
            Assert.False(pool.GetBit(snaphot, i, j), "bit ({0}, {1}) is expected to be unset", i, j);
        }
      }
    }
  }
}
