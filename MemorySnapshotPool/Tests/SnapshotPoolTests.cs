using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;
using NUnit.Framework;

namespace MemorySnapshotPool.Tests
{
  [TestFixture]
  public class SnapshotPoolTests
  {
    private const int ElementsPerSnapshot = 10;

    [Test]
    public void ZeroModifications()
    {
      var snapshotPool = new SnapshotPool(bytesPerSnapshot: ElementsPerSnapshot * sizeof(uint));
      var zeroSnapshot = SnapshotPool.ZeroSnapshot;

      var zeroArray = snapshotPool.SnapshotToDebugArray(zeroSnapshot);
      Assert.That(zeroArray, Is.All.Zero);
      Assert.AreEqual(zeroArray.Length, ElementsPerSnapshot);

      for (var index = 0; index < ElementsPerSnapshot; index++)
      {
        Assert.That(snapshotPool.GetUint32(zeroSnapshot, index), Is.Zero);
      }

      var modifiedHandle = snapshotPool.SetSingleUInt32(zeroSnapshot, 0, valueToSet: 0);
      Assert.AreEqual(zeroSnapshot, modifiedHandle);

      var modifiedHandle2 = snapshotPool.SetSingleUInt32(zeroSnapshot, 9, valueToSet: 0);
      Assert.AreEqual(zeroSnapshot, modifiedHandle2);

      snapshotPool.CopyToSharedSnapshot(zeroSnapshot);

      var modifiedHandle3 = snapshotPool.StoreSharedSnapshot();
      Assert.AreEqual(zeroSnapshot, modifiedHandle3);

      snapshotPool.SetSharedSnapshotUint32(1, valueToSet: 0);
      snapshotPool.SetSharedSnapshotUint32(3, valueToSet: 0);

      var modifiedHandle4 = snapshotPool.StoreSharedSnapshot();
      Assert.AreEqual(zeroSnapshot, modifiedHandle4);
    }

    [Test]
    public void ZeroBytes()
    {
      var snapshotPool = new SnapshotPool(bytesPerSnapshot: 0);
      var zeroSnapshot = SnapshotPool.ZeroSnapshot;

      Assert.That(snapshotPool.SnapshotToDebugArray(zeroSnapshot), Is.Empty);
    }

    [Test]
    public void Intern()
    {
      var snapshotPool = new SnapshotPool(bytesPerSnapshot: ElementsPerSnapshot * sizeof(uint));
      var zeroSnapshot = SnapshotPool.ZeroSnapshot;

      var modifiedSnapshot = snapshotPool.SetSingleUInt32(zeroSnapshot, elementIndex: 5, valueToSet: 42);
      Assert.AreNotEqual(zeroSnapshot, modifiedSnapshot);

      var modifiedArray = snapshotPool.SnapshotToDebugArray(modifiedSnapshot);
      Assert.AreEqual(modifiedArray[5], 42);

      var modifiedSnapshot2 = snapshotPool.SetSingleUInt32(zeroSnapshot, elementIndex: 5, valueToSet: 42);
      Assert.AreEqual(modifiedSnapshot, modifiedSnapshot2);

      var modifiedSnapshot3 = snapshotPool.SetSingleUInt32(modifiedSnapshot2, elementIndex: 5, valueToSet: 0);
      Assert.AreEqual(zeroSnapshot, modifiedSnapshot3);

      snapshotPool.CopyToSharedSnapshot(zeroSnapshot);
      snapshotPool.SetSharedSnapshotUint32(elementIndex: 5, valueToSet: 42);

      var modifiedSnapshot4 = snapshotPool.StoreSharedSnapshot();
      Assert.AreEqual(modifiedSnapshot, modifiedSnapshot4);
    }

    [Test]
    public void PoolAllocationlessResize()
    {
      var rows = (
          from a in Enumerable.Range(0, 10)
          from b in Enumerable.Range(0, 10)
          from c in Enumerable.Range(0, 10)
          from d in Enumerable.Range(0, 10)
          select new[] {(uint) a, (uint) b, (uint) c, (uint) d}
        ).ToList();

      var snapshotPool = new SnapshotPool(bytesPerSnapshot: 4 * sizeof(uint), capacity: rows.Count);
      var handles = CreateHashSetWithCapacity(rows.Count);

      var totalMemoryBefore = GC.GetTotalMemory(false);

      foreach (var row in rows)
      {
        for (var index = 0; index < row.Length; index++)
          snapshotPool.SetSharedSnapshotUint32(index, row[index]);

        var modifiedSnapshot = snapshotPool.StoreSharedSnapshot();
        AllocationlessAssert(handles.Add(modifiedSnapshot));
      }

      var totalMemoryAfter = GC.GetTotalMemory(false);
      AllocationlessAssert(totalMemoryBefore == totalMemoryAfter);
      AllocationlessAssert(rows.Count == handles.Count);

      var snapshot = SnapshotPool.ZeroSnapshot;

      foreach (var row in rows)
      {
        for (var index = 0; index < row.Length; index++)
        {
          snapshot = snapshotPool.SetSingleUInt32(snapshot, index, row[index]);
          AllocationlessAssert(handles.Contains(snapshot));
        }
      }

      var totalMemoryAfter2 = GC.GetTotalMemory(false);
      AllocationlessAssert(totalMemoryBefore == totalMemoryAfter2);
    }

    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    private static void AllocationlessAssert(bool condition)
    {
      if (!condition)
        throw new AssertionException("Condition is 'false'");
    }

    [NotNull]
    private static HashSet<SnapshotHandle> CreateHashSetWithCapacity(int capacity)
    {
      var handles = new HashSet<SnapshotHandle>();

      for (var index = 0; index < capacity; index++)
        handles.Add(new SnapshotHandle(index));

      handles.Clear();
      return handles;
    }
  }
}