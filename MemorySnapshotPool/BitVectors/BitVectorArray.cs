using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace MemorySnapshotPool
{
  [StructLayout(LayoutKind.Auto)]
  public struct BitVectorArray
  {
    private const int VectorItemSize = 32; // need to review all methods if this constant is changed

    private readonly short myBitsPerItem;
    [NotNull] private readonly uint[] myVector;
    private int myHashCode;

    public BitVectorArray(short items, short bitsPerItem)
    {
      myBitsPerItem = bitsPerItem;
      var size = (items * bitsPerItem - 1) / VectorItemSize + 1;
      myVector = new uint[size];
      myHashCode = CalculateHashCode(myVector);
    }

    private BitVectorArray(BitVectorArray other)
    {
      var size = other.myVector.Length;
      myVector = new uint[size];
      myBitsPerItem = other.myBitsPerItem;
      Array.Copy(other.myVector, myVector, size);
      myHashCode = -1;
    }

    [Pure]
    public bool GetBit(int item, int bit)
    {
      var index = item * myBitsPerItem + bit;
      var vectorItem = myVector[index / VectorItemSize];
      return (vectorItem & (1 << (index % VectorItemSize))) != 0;
    }

    [Pure]
    public bool SetBit(int item, int bit, out BitVectorArray result)
    {
      result = this;
      return SetBitInternal(item, bit, ref result);
    }

    private bool SetBitInternal(int item, int bit, ref BitVectorArray result)
    {
      var index = item * myBitsPerItem + bit;
      var mask = (uint) 1 << (index % VectorItemSize);
      if ((myVector[index / VectorItemSize] & mask) != 0)
        return false;

      result = new BitVectorArray(this);
      result.myVector[index / VectorItemSize] |= mask;
      return true;
    }

    [Pure]
    public bool SetBitAndClearOtherBits(int[] items, int bit, out BitVectorArray result)
    {
      result = this;

      var copied = false;
      foreach (var item in items)
      {
        copied = copied | SetBitAndClearOtherBitsInternal(item, bit, ref result, !copied);
      }

      return copied;
    }

    [Pure]
    public bool SetBitAndClearOtherBits(int item, int bit, out BitVectorArray result)
    {
      result = this;
      return SetBitAndClearOtherBitsInternal(item, bit, ref result, true);
    }

    public bool SetBitAndClearOtherBitsInternal(int item, int bit, ref BitVectorArray result, bool copyOnChange)
    {
      var index = item * myBitsPerItem + bit;
      var mask = ((uint)1) << (index % VectorItemSize);
      index = index / VectorItemSize;

      if ((myVector[index] & mask) != 0)
      {
        myVector[index] &= ~mask;
        var copied = Clear(item, ref result, true);
        myVector[index] |= mask;
        result.myVector[index] |= mask;
        return copied;
      }

      if (copyOnChange)
        result = new BitVectorArray(this);

      Clear(item, ref result, false);
      result.myVector[index] |= mask;
      return copyOnChange;
    }

    [Pure]
    public bool Clear([NotNull] int[] items, out BitVectorArray result)
    {
      var changed = false;
      result = this;
      foreach (var item in items)
      {
        changed = changed | Clear(item, ref result, !changed);
      }

      return changed;
    }

    [Pure]
    public bool Clear(int item, out BitVectorArray result)
    {
      result = this;
      return Clear(item, ref result, true);
    }

    private bool Clear(int item, ref BitVectorArray result, bool copyOnChange)
    {
      var firstBitIndex = item * myBitsPerItem;
      var firstIndex = firstBitIndex / VectorItemSize;
      var lastIndex = (firstBitIndex + myBitsPerItem - 1) / VectorItemSize;

      firstBitIndex = firstBitIndex % VectorItemSize;
      var lastIndx = (firstBitIndex + myBitsPerItem) % VectorItemSize;
      var k = Math.Min(VectorItemSize, firstBitIndex + myBitsPerItem);

      var maskForFirstElement = uint.MaxValue >> (VectorItemSize - k + firstBitIndex);
      maskForFirstElement <<= firstBitIndex;

      uint maskForLastElement;
      if (firstIndex == lastIndex)
        maskForLastElement = maskForFirstElement;
      else
        maskForLastElement = (((uint)1) << lastIndx) - 1;

      var vector = myVector;
      if ((vector[firstIndex] & maskForFirstElement) == 0)
      {
        firstIndex++;
        maskForFirstElement = uint.MaxValue;
        var allBitsClear = true;
        for (; firstIndex < lastIndex; firstIndex++)
        {
          if (vector[firstIndex] != 0)
          {
            allBitsClear = false;
            break;
          }
        }

        allBitsClear &= (vector[lastIndex] & maskForLastElement) == 0;
        if (allBitsClear)
          return false;
      }

      if (copyOnChange)
        result = new BitVectorArray(this);

      if (firstIndex < lastIndex)
        result.myVector[firstIndex] = (vector[firstIndex] & ~maskForFirstElement);

      for (var i = firstIndex + 1; i < lastIndex; i++)
        result.myVector[i] = 0;

      result.myVector[lastIndex] &= ~maskForLastElement;
      return copyOnChange;
    }

    [Pure]
    public bool Copy(int from, int to, out BitVectorArray result)
    {
      result = this;
      var copied = false;

      var fromIndex = from * myBitsPerItem;
      var fromIndexLow = fromIndex % VectorItemSize;
      var fromIndexHigh = fromIndex / VectorItemSize;
      var fromValue = myVector[fromIndexHigh];

      var toIndex = to * myBitsPerItem;
      var toIndexLow = toIndex % VectorItemSize;
      var toIndexHigh = toIndex / VectorItemSize;
      var toValue = myVector[toIndexHigh];

      for (var index = 0; index < myBitsPerItem; index++, fromIndexLow++, toIndexLow++)
      {
        if (fromIndexLow == VectorItemSize)
        {
          fromIndexLow = 0;
          fromIndexHigh++;
          fromValue = myVector[fromIndexHigh];
        }

        if (toIndexLow == VectorItemSize)
        {
          toIndexLow = 0;
          toIndexHigh++;
          toValue = myVector[toIndexHigh];
        }

        var fromMask = ((uint)1) << fromIndexLow;
        var fromBit = (fromValue & fromMask) != 0;

        var toMask = ((uint)1) << toIndexLow;
        var toBit = (toValue & toMask) != 0;

        if (fromBit ^ toBit)
        {
          if (!copied)
          {
            result = new BitVectorArray(this);
            copied = true;
          }

          if (fromBit)
            result.myVector[toIndexHigh] |= toMask;
          else
            result.myVector[toIndexHigh] &= ~toMask;
        }
      }

      return copied;
    }

    [Pure]
    public BitVector64 GetItem64(int item)
    {
      Debug.Assert(myBitsPerItem <= 64, "myBitsPerItem <= 64");

      var lowerBound = item * myBitsPerItem;
      var upperBound = lowerBound + myBitsPerItem;
      var lowerIndex = lowerBound / VectorItemSize;
      var upperIndex = (upperBound - 1) / VectorItemSize;
      var lowerShift = lowerBound % VectorItemSize;

      if (lowerIndex == upperIndex)
      {
        var value = myVector[lowerIndex];
        var mask = uint.MaxValue >> (VectorItemSize - myBitsPerItem);
        var vector = (value >> lowerShift) & mask;

        return new BitVector64(vector);
      }

      var lowerValue = myVector[lowerIndex];
      var lowerDelta = VectorItemSize - lowerShift;
      ulong lowerPart = lowerValue >> lowerShift;

      var upperValue = myVector[upperIndex];
      var upperShift = upperBound % VectorItemSize;
      var upperMask = uint.MaxValue >> (VectorItemSize - upperShift);
      ulong upperPart = upperValue & upperMask;

      var middleIndex = lowerIndex + 1;
      if (middleIndex == upperIndex)
      {
        var vector = lowerPart | (upperPart << lowerDelta);

        return new BitVector64(vector);
      }
      else
      {
        ulong middlePart = myVector[middleIndex];
        var vector = lowerPart | (middlePart << lowerDelta) | (upperPart << (lowerDelta + VectorItemSize));

        return new BitVector64(vector);
      }
    }

    [Pure]
    public bool SetItem64(BitVector64 value, int item, out BitVectorArray result)
    {
      Debug.Assert(myBitsPerItem <= 64, "myBitsPerItem <= 64");

      var lowerBound = item * myBitsPerItem;
      var upperBound = lowerBound + myBitsPerItem;
      var lowerIndex = lowerBound / VectorItemSize;
      var upperIndex = (upperBound - 1) / VectorItemSize;
      var lowerShift = lowerBound % VectorItemSize;

      if (lowerIndex == upperIndex)
      {
        var newVector = unchecked((uint)value.Vector);

        var oldValue = myVector[lowerIndex];
        var mask = uint.MaxValue >> (VectorItemSize - myBitsPerItem);

        var oldVector = (oldValue >> lowerShift) & mask;
        if (oldVector == newVector)
        {
          result = this;
          return false;
        }

        var clearedValue = oldValue & ~(mask << lowerShift);
        var newValue = clearedValue | (newVector << lowerShift);

        result = new BitVectorArray(this);
        result.myVector[lowerIndex] = newValue;
        return true;
      }
      else
      {
        var lowerValue = myVector[lowerIndex];
        var lowerDelta = VectorItemSize - lowerShift;
        ulong lowerPart = lowerValue >> lowerShift;

        var upperValue = myVector[upperIndex];
        var upperShift = upperBound % VectorItemSize;
        var upperMask = uint.MaxValue >> (VectorItemSize - upperShift);
        ulong upperPart = upperValue & upperMask;

        ulong oldVector;
        int upperDelta;

        var middleIndex = lowerIndex + 1;
        if (middleIndex == upperIndex)
        {
          oldVector = lowerPart | (upperPart << lowerDelta);
          upperDelta = lowerDelta;
        }
        else
        {
          ulong middlePart = myVector[middleIndex];
          upperDelta = lowerDelta + VectorItemSize;
          oldVector = lowerPart | (middlePart << lowerDelta) | (upperPart << upperDelta);
        }

        var newVector = value.Vector;
        if (newVector == oldVector)
        {
          result = this;
          return false;
        }

        var clearedLowerValue = lowerValue & ~(uint.MaxValue << lowerShift);
        var newLowerMask = unchecked((uint)newVector) << lowerShift;
        var newLowerValue = clearedLowerValue | newLowerMask;

        var clearedUpperValue = upperValue & ~upperMask;
        var newUpperMask = unchecked((uint)(newVector >> upperDelta));
        var newUpperValue = clearedUpperValue | newUpperMask;

        result = new BitVectorArray(this);
        result.myVector[lowerIndex] = newLowerValue;
        result.myVector[upperIndex] = newUpperValue;

        if (middleIndex != upperIndex)
        {
          var newMiddleValue = unchecked((uint)(newVector >> lowerDelta));
          result.myVector[middleIndex] = newMiddleValue;
        }

        return true;
      }
    }

    public static bool operator ==(BitVectorArray left, BitVectorArray right)
    {
      var leftVector = left.myVector;
      var rightVector = right.myVector;

      Debug.Assert(leftVector.Length == rightVector.Length, "left.myVector.Length == right.myVector.Length");
      Debug.Assert(left.myBitsPerItem == right.myBitsPerItem, "left.myBitsPerItem == right.myBitsPerItem");

      if (ReferenceEquals(leftVector, rightVector)) return true;

      var size = leftVector.Length;
      for (var index = 0; index < size; index++)
      {
        if (leftVector[index] != rightVector[index])
          return false;
      }

      return true;
    }

    public static bool operator !=(BitVectorArray left, BitVectorArray right)
    {
      var leftVector = left.myVector;
      var rightVector = right.myVector;

      Debug.Assert(leftVector.Length == rightVector.Length, "leftVector.Length == rightVector.Length");
      Debug.Assert(left.myBitsPerItem == right.myBitsPerItem, "left.myBitsPerItem == right.myBitsPerItem");

      if (ReferenceEquals(leftVector, rightVector)) return false;

      var size = leftVector.Length;
      for (var index = 0; index < size; index++)
      {
        if (leftVector[index] != rightVector[index])
          return true;
      }

      return false;
    }

    public override bool Equals(object obj)
    {
      throw new InvalidOperationException("Should not be called!");
    }

    [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
    public override int GetHashCode()
    {
      if (myHashCode == -1)
      {
        myHashCode = CalculateHashCode(myVector);
      }

      return myHashCode;
    }

    private static int CalculateHashCode([NotNull] uint[] values)
    {
      unchecked
      {
        uint code = 0;
        foreach (var item in values)
        {
          code = code * 397 ^ item;
        }

        return (int)code;
      }
    }

    [Pure]
    public IList<int> Bits(int item)
    {
      var result = new List<int>();

      var bitsPerItem = myBitsPerItem;
      var pos = item * bitsPerItem;

      for (var index = 0; index < bitsPerItem; index++, pos++)
      {
        var i = myVector[pos / VectorItemSize];
        if ((i & (1 << (pos % VectorItemSize))) != 0)
          result.Add(index);
      }

      return result;
    }
  }
}