using System;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using Microsoft.Win32.SafeHandles;

namespace MemorySnapshotPool
{
  // todo: can work over pinned managed array as well

  public unsafe struct UnmanagedSnapshotStorage : ISnapshotStorage
  {
    private readonly uint myIntsPerSnapshot;
    private uint myCurrentCapacity;
    private uint myLastUsedHandle;

    [NotNull] private readonly UnmanagedMemoryHandle myMemoryHandle;
    private uint* myMemory;

    public UnmanagedSnapshotStorage(uint intsPerSnapshot, uint capacity)
    {
      myIntsPerSnapshot = intsPerSnapshot;
      myCurrentCapacity = intsPerSnapshot * capacity;

      myMemoryHandle = new UnmanagedMemoryHandle(numberOfBytes: myCurrentCapacity * sizeof(uint));
      myMemory = (uint*) myMemoryHandle.DangerousGetHandle();

      myLastUsedHandle = 2; // shared + zero
      ZeroMemory(myMemory, myIntsPerSnapshot * 2);
    }

    public uint MemoryConsumptionTotalInBytes
    {
      get { return myCurrentCapacity * sizeof(uint); }
    }

    public uint GetUint32(SnapshotHandle snapshot, uint elementIndex)
    {
      return myMemory[snapshot.Handle * myIntsPerSnapshot + elementIndex];
    }

    public bool CompareRange(SnapshotHandle snapshot1, SnapshotHandle snapshot2, uint startIndex, uint endIndex)
    {
      var ptr1 = myMemory + snapshot1.Handle * myIntsPerSnapshot;
      var ptr2 = myMemory + snapshot2.Handle * myIntsPerSnapshot;

      for (var index = startIndex; index < endIndex; index++)
      {
        if (ptr1[index] != ptr2[index]) return false;
      }

      return true;
    }

    public void Copy(SnapshotHandle sourceSnapshot, SnapshotHandle targetSnapshot)
    {
      var sourcePtr = myMemory + sourceSnapshot.Handle * myIntsPerSnapshot;
      var targetPtr = myMemory + targetSnapshot.Handle * myIntsPerSnapshot;

      for (var index = 0; index < myIntsPerSnapshot; index++)
      {
        targetPtr[index] = sourcePtr[index];
      }
    }

    public void MutateUint32(SnapshotHandle snapshot, uint elementIndex, uint value)
    {
      myMemory[snapshot.Handle * myIntsPerSnapshot + elementIndex] = value;
    }

    public SnapshotHandle AllocNewHandle()
    {
      var offset = (myLastUsedHandle + 1) * myIntsPerSnapshot;
      if (offset > myCurrentCapacity)
      {
        myCurrentCapacity *= 2;
        myMemory = myMemoryHandle.Resize(myCurrentCapacity * sizeof(uint));
      }

      return new SnapshotHandle(myLastUsedHandle++);
    }

    private static void ZeroMemory(uint* ptr, uint count)
    {
      for (var index = 0; index < count; index++)
      {
        ptr[index] = 0;
      }
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