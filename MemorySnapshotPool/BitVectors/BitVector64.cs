using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace MemorySnapshotPool
{
  [StructLayout(LayoutKind.Auto)]
  public struct BitVector64 : IEquatable<BitVector64>
  {
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
    private ulong myVector; // note: do not make it readonly

    public static readonly BitVector64 Empty = default(BitVector64);

    internal BitVector64(ulong vector)
    {
      myVector = vector;
    }

    internal ulong Vector
    {
      get { return myVector; }
    }

    [Pure]
    public bool GetBit(int index)
    {
      Debug.Assert(index >= 0 && index < 64, "index >= 0 && index < 64");

      return (myVector & ((ulong)1 << index)) != 0;
    }

    [Pure]
    public BitVector64 SetBit(int index)
    {
      Debug.Assert(index >= 0 && index < 64, "index >= 0 && index < 64");

      return new BitVector64(myVector | ((ulong)1 << index));
    }

    [Pure]
    public BitVector64 SetBitAndClearOther(int index)
    {
      return new BitVector64((ulong)1 << index);
    }

    [Pure]
    public IList<int> Bits()
    {
      if (myVector == 0) return new List<int>();

      var result = new List<int>(capacity: NumberOfSetBits());

      for (var index = 0; index < 64; index++)
      {
        if ((myVector & ((ulong)1 << index)) != 0)
          result.Add(index);
      }

      return result;
    }

    private int NumberOfSetBits()
    {
      unchecked
      {
        var i = myVector;
        i = i - ((i >> 1) & 0x5555555555555555UL);
        i = (i & 0x3333333333333333UL) + ((i >> 2) & 0x3333333333333333UL);
        return (int)(((i + (i >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL >> 56);
      }
    }

    public bool Equals(BitVector64 other)
    {
      return myVector == other.myVector;
    }

    public override bool Equals(object obj)
    {
      throw new InvalidOperationException("Do not box me plz");
    }

    [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
    public override int GetHashCode()
    {
      return myVector.GetHashCode();
    }

    public static bool operator ==(BitVector64 left, BitVector64 right)
    {
      return left.Equals(right);
    }

    public static bool operator !=(BitVector64 left, BitVector64 right)
    {
      return !left.Equals(right);
    }
  }
}