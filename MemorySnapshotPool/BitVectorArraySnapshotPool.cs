using System;
using System.Diagnostics;
using JetBrains.Annotations;

namespace MemorySnapshotPool
{
  public class BitVectorArraySnapshotPool : SnapshotPool
  {
    private readonly uint myBitsPerItem;
    private readonly uint myArrayLength;

    // can we change the whole array item via single uint32 write?
    private readonly bool myCanModifySingleItemAtomically;

    private const int VectorItemSize = 32;

    public BitVectorArraySnapshotPool(uint arrayLength, uint bitsPerItem, uint capacity = 100)
      : base(BytesPerItem(arrayLength, bitsPerItem), capacity)
    {
      myArrayLength = arrayLength;
      myBitsPerItem = bitsPerItem;
      myCanModifySingleItemAtomically = bitsPerItem <= VectorItemSize && VectorItemSize % bitsPerItem == 0;
    }

    private static uint BytesPerItem(uint arrayLength, uint bitsPerItem)
    {
      return (arrayLength * bitsPerItem - 1) / 8 + 1;
    }

    public uint ArrayLength { get { return myArrayLength; } }
    public uint BitsPerItem { get { return myBitsPerItem; } }

    [Pure]
    public bool GetBit(SnapshotHandle snapshot, uint index, uint bit)
    {
      Debug.Assert(index < myArrayLength, "index < myArrayLength");
      Debug.Assert(bit < myBitsPerItem, "bit < myBitsPerItem");

      var bitIndex = index * myBitsPerItem + bit;
      var vectorItem = GetUint32(snapshot, bitIndex / VectorItemSize);
      return (vectorItem & (1u << (int)(bitIndex % VectorItemSize))) != 0;
    }

    [MustUseReturnValue]
    public SnapshotHandle SetBit(SnapshotHandle snapshot, uint index, uint bit)
    {
      Debug.Assert(index < myArrayLength, "index < myArrayLength");
      Debug.Assert(bit < myBitsPerItem, "bit < myBitsPerItem");

      var bitIndex = index * myBitsPerItem + bit;
      var mask = 1u << (int) (bitIndex % VectorItemSize);

      var vectorItem = GetUint32(snapshot, bitIndex / VectorItemSize);
      if ((vectorItem & mask) != 0)
        return snapshot;

      return SetSingleUInt32(snapshot, bitIndex / VectorItemSize, vectorItem | mask);
    }

    public void SetSharedSnapshotBit(uint index, uint bit)
    {
      Debug.Assert(index < myArrayLength, "index < myArrayLength");
      Debug.Assert(bit < myBitsPerItem, "bit < myBitsPerItem");

      var bitIndex = index * myBitsPerItem + bit;
      var mask = 1u << (int)(bitIndex % VectorItemSize);

      var vectorItem = GetUint32(SharedSnapshot, bitIndex / VectorItemSize);
      if ((vectorItem & mask) != 0) return;

      SetSharedSnapshotUint32(bitIndex / VectorItemSize, vectorItem | mask);
    }

    [MustUseReturnValue]
    public SnapshotHandle Clear(SnapshotHandle snapshot, [NotNull] uint[] indexes)
    {
      var currentSnapshot = snapshot;

      foreach (var index in indexes)
      {
        Debug.Assert(index < myArrayLength, "index < myArrayLength");

        currentSnapshot = Clear(currentSnapshot, index, useSharedSnapshotToModifyAndLeaveItThere: true);
      }

      if (currentSnapshot == SharedSnapshot)
        return StoreSharedSnapshot();

      return currentSnapshot;
    }

    [MustUseReturnValue]
    public SnapshotHandle Clear(SnapshotHandle snapshot, uint index)
    {
      Debug.Assert(index < myArrayLength, "index < myArrayLength");

      var resultingSnapshot = Clear(snapshot, index, useSharedSnapshotToModifyAndLeaveItThere: false);
      if (resultingSnapshot == SharedSnapshot)
        return StoreSharedSnapshot();

      return resultingSnapshot;
    }

    private SnapshotHandle Clear(SnapshotHandle snapshot, uint index, bool useSharedSnapshotToModifyAndLeaveItThere)
    {
      var firstBitIndex = index * myBitsPerItem;
      var firstIndex = firstBitIndex / VectorItemSize;
      var lastIndex = (firstBitIndex + myBitsPerItem - 1) / VectorItemSize;

      firstBitIndex = firstBitIndex % VectorItemSize;
      var lastIndx = (firstBitIndex + myBitsPerItem) % VectorItemSize;
      var k = Math.Min(VectorItemSize, firstBitIndex + myBitsPerItem);

      var maskForFirstElement = uint.MaxValue >> (int) (VectorItemSize - k + firstBitIndex);
      maskForFirstElement <<= (int) firstBitIndex;

      uint maskForLastElement;
      if (firstIndex == lastIndex)
        maskForLastElement = maskForFirstElement;
      else
        maskForLastElement = (1u << (int) lastIndx) - 1;

      if ((GetUint32(snapshot, firstIndex) & maskForFirstElement) == 0u)
      {
        firstIndex++;
        maskForFirstElement = uint.MaxValue;

        var allBitsClear = true;
        for (; firstIndex < lastIndex; firstIndex++)
        {
          if (GetUint32(snapshot, firstIndex) != 0u)
          {
            allBitsClear = false;
            break;
          }
        }

        if (allBitsClear)
        {
          allBitsClear = (GetUint32(snapshot, lastIndex) & maskForLastElement) == 0u;
        }

        if (allBitsClear) return snapshot;
      }

      if (!useSharedSnapshotToModifyAndLeaveItThere && myCanModifySingleItemAtomically)
      {
        Debug.Assert(firstIndex == lastIndex, "firstIndex == lastIndex");

        var single = GetUint32(snapshot, firstIndex);
        return SetSingleUInt32(snapshot, firstIndex, single & ~maskForFirstElement & ~maskForLastElement);
      }

      if (snapshot != SharedSnapshot)
        LoadToSharedSnapshot(snapshot);

      if (firstIndex < lastIndex)
      {
        var first = GetUint32(SharedSnapshot, firstIndex);
        SetSharedSnapshotUint32(firstIndex, first & ~maskForFirstElement);
      }

      for (var i = firstIndex + 1; i < lastIndex; i++)
      {
        SetSharedSnapshotUint32(i, 0u);
      }

      var last = GetUint32(SharedSnapshot, lastIndex);
      SetSharedSnapshotUint32(lastIndex, last & ~maskForLastElement);

      return SharedSnapshot;
    }

    public void SetBitAndClearOtherBits(SnapshotHandle initialSnapshot, int i, int i1)
    {
      throw new NotImplementedException();
    }

    [Pure]
    public bool SetBitAndClearOtherBits([NotNull] uint[] items, int typeIndex, out BitVectorArray result)
    {
      result = this;

      var copied = false;
      foreach (var item in items)
      {
        copied = copied | SetBitAndClearOtherBitsInternal(item, typeIndex, ref result, !copied);
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
  }
}