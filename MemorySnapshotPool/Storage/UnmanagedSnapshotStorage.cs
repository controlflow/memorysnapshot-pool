using System;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using Microsoft.Win32.SafeHandles;

namespace MemorySnapshotPool.Storage
{
  // todo: can work over pinned managed array as well

  public unsafe struct UnmanagedSnapshotStorage : ISnapshotStorage
  {
    private uint myCurrentCapacity;
    private uint myLastUsedOffset;

    [NotNull] private UnmanagedMemoryHandle myMemoryHandle;
    private uint* myMemory;

    public void Initialize(uint capacityInInts)
    {
      if (myMemoryHandle == null)
        throw new InvalidOperationException("Already initialized");
      
      myCurrentCapacity = capacityInInts;

      myMemoryHandle = new UnmanagedMemoryHandle(numberOfBytes: myCurrentCapacity * sizeof(uint));
      myMemory = (uint*) myMemoryHandle.DangerousGetHandle();

      myLastUsedOffset = 0;
    }

    public uint MemoryConsumptionTotalInBytes => myCurrentCapacity * sizeof(uint);

    public uint GetUint32(SnapshotHandle snapshot, uint elementIndex)
    {
      return myMemory[snapshot.Handle + elementIndex];
    }

    public bool CompareRange(SnapshotHandle snapshot1, SnapshotHandle snapshot2, uint startIndex, uint endIndex)
    {
      var ptr1 = myMemory + snapshot1.Handle;
      var ptr2 = myMemory + snapshot2.Handle;

      for (var index = startIndex; index < endIndex; index++)
      {
        if (ptr1[index] != ptr2[index]) return false;
      }

      return true;
    }

    public void Copy(SnapshotHandle sourceSnapshot, SnapshotHandle targetSnapshot, uint intsToCopy)
    {
      var sourcePtr = myMemory + sourceSnapshot.Handle;
      var targetPtr = myMemory + targetSnapshot.Handle;

      for (var index = 0; index < intsToCopy; index++)
      {
        targetPtr[index] = sourcePtr[index];
      }
    }

    public void MutateUint32(SnapshotHandle snapshot, uint elementIndex, uint value)
    {
      myMemory[snapshot.Handle + elementIndex] = value;
    }

    public SnapshotHandle AllocateNewHandle(uint intsToAllocate)
    {
      var lastOffsetUsed = myLastUsedOffset;
      
      var newLastOffset = lastOffsetUsed + intsToAllocate;
      if (newLastOffset > myCurrentCapacity)
      {
        myCurrentCapacity *= 2;
        myMemory = myMemoryHandle.Resize(myCurrentCapacity * sizeof(uint));
      }

      myLastUsedOffset = newLastOffset;
      return new SnapshotHandle(lastOffsetUsed);
    }

    private sealed class UnmanagedMemoryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
      public UnmanagedMemoryHandle(uint numberOfBytes) : base(ownsHandle: true)
      {
        // todo: can return -1 if too big?

        SetHandle(Marshal.AllocHGlobal(cb: (int) numberOfBytes));
      }

      public uint* Resize(uint numberOfBytes)
      {
        handle = Marshal.ReAllocHGlobal(handle, (IntPtr) numberOfBytes);
        return (uint*) handle;
      }

      protected override bool ReleaseHandle()
      {
        if (handle != IntPtr.Zero)
        {
          Marshal.FreeHGlobal(handle);
          handle = IntPtr.Zero;
          return true;
        }

        return false;
      }
    }
  }
}