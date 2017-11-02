using NUnit.Framework;

namespace MemorySnapshotPool.Tests
{
  [TestFixture]
  public class BitVector64Test
  {
    [Test]
    public void TestSetBit()
    {
      var vector = BitVector64.Empty;

      var result = vector.SetBit(index: 0);
      Assert.That(result.GetBit(0), Is.True);
      Assert.That(result.GetBit(1), Is.False);
      Assert.That(result.GetBit(63), Is.False);

      var result2 = result.SetBitAndClearOther(index: 3).SetBit(index: 56);
      Assert.That(result2.GetBit(0), Is.False);
      Assert.That(result2.GetBit(1), Is.False);
      Assert.That(result2.GetBit(2), Is.False);
      Assert.That(result2.GetBit(3), Is.True);
      Assert.That(result2.GetBit(56), Is.True);
      Assert.That(result2.GetBit(57), Is.False);

      var result3 = result.SetBit(index: 4).SetBit(45);
      Assert.That(result3.Bits(), Is.EquivalentTo(new[] { 0, 4, 45 }));
    }

    [Test]
    public void TestEquality()
    {
      var vector1 = BitVector64.Empty.SetBit(index: 1).SetBit(index: 45);
      var vector2 = BitVector64.Empty.SetBit(index: 1).SetBit(index: 45);

      Assert.That(vector1, Is.EqualTo(vector2));
      Assert.That(vector1.GetHashCode(), Is.EqualTo(vector2.GetHashCode()));
    }

    [Test]
    public void TestBitVectorArrayCopy01()
    {
      var array = new BitVectorArray(items: 100, bitsPerItem: 2);
      Assert.That(array.SetBit(item: 0, bit: 1, result: out array), Is.True);
      Assert.That(array.SetBit(item: 1, bit: 0, result: out array), Is.True);

      Assert.That(array.Bits(item: 0), Is.EquivalentTo(new[] { 1 }));
      Assert.That(array.Bits(item: 1), Is.EquivalentTo(new[] { 0 }));

      var item1 = array.GetItem64(item: 0);
      Assert.That(item1.Bits(), Is.EquivalentTo(new[] { 1 }));

      var item2 = array.GetItem64(item: 1);
      Assert.That(item2.Bits(), Is.EquivalentTo(new[] { 0 }));

      var array2 = new BitVectorArray(items: 100, bitsPerItem: 2);
      // write some garbage to check overwrite
      Assert.That(array2.SetBit(item: 1, bit: 1, result: out array2), Is.True);
      Assert.That(array2.SetBit(item: 1, bit: 0, result: out array2), Is.True);

      // write items back
      Assert.That(array2.SetItem64(value: item1, item: 0, result: out array2), Is.True);
      Assert.That(array2.SetItem64(value: item2, item: 1, result: out array2), Is.True);

      Assert.That(array == array2, Is.True);

      Assert.That(array2.SetBit(item: 32, bit: 0, result: out array2), Is.True);
      Assert.That(array2.SetBit(item: 47, bit: 1, result: out array2), Is.True);

      var item32 = array2.GetItem64(item: 32);
      var item47 = array2.GetItem64(item: 47);
      Assert.That(array.SetItem64(value: item32, item: 32, result: out array), Is.True);
      Assert.That(array.SetItem64(value: item32, item: 32, result: out array), Is.False);
      Assert.That(array.SetItem64(value: item47, item: 47, result: out array), Is.True);
      Assert.That(array.SetItem64(value: item47, item: 47, result: out array), Is.False);

      Assert.That(array == array2, Is.True);
    }

    [Test]
    public void TestBitVectorArrayCopy02()
    {
      foreach (var size in new short[] { 42, 57, 64 })
        for (var i = 0; i < 20; i++)
        {
          var array = new BitVectorArray(items: 20, bitsPerItem: size);
          Assert.That(array.SetBit(item: i, bit: 0, result: out array), Is.True);
          Assert.That(array.SetBit(item: i, bit: 4, result: out array), Is.True);
          Assert.That(array.SetBit(item: i, bit: 15, result: out array), Is.True);
          Assert.That(array.SetBit(item: i, bit: 31, result: out array), Is.True);
          Assert.That(array.SetBit(item: i, bit: 32, result: out array), Is.True);
          Assert.That(array.SetBit(item: i, bit: 41, result: out array), Is.True);

          var item = array.GetItem64(item: i);
          var actual = item.Bits();
          var expected = array.Bits(item: i);
          Assert.That(actual, Is.EquivalentTo(expected));

          Assert.That(array.SetItem64(value: item, item: i, result: out array), Is.False);

          var array2 = new BitVectorArray(items: 20, bitsPerItem: size);
          Assert.That(array2.SetItem64(value: item, item: i, result: out array2), Is.True);
          Assert.That(array2.SetItem64(value: item, item: i, result: out array2), Is.False);

          Assert.That(array.Bits(item: i), Is.EquivalentTo(array2.Bits(item: i)));
          Assert.That(array == array2, Is.True);
        }
    }

    [Test]
    public void TestBitVectorArrayCopy03()
    {
      for (short size = 1; size < 64; size++)
      {
        const int itemsCount = 10;
        for (short item = 1; item < itemsCount; item++)
        {
          for (var bit = 0; bit < size; bit++)
          {
            var array = new BitVectorArray(items: itemsCount, bitsPerItem: size);
            var array2 = new BitVectorArray(items: itemsCount, bitsPerItem: size);

            Assert.That(array.SetBit(item, bit, out array), Is.True);

            var vector64 = array.GetItem64(item);
            Assert.That(vector64.GetBit(bit), Is.True);
            Assert.That(vector64.Bits(), Is.EquivalentTo(new[] { bit }));

            Assert.That(array.SetItem64(vector64, item, out array), Is.False);

            Assert.That(array2.SetItem64(vector64, item, out array2), Is.True);
            Assert.That(array == array2, Is.True);
          }
        }
      }
    }
  }
}