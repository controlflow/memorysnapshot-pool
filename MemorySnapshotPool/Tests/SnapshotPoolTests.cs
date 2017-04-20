using System;
using System.Collections.Generic;
using System.Linq;
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

      var modifiedHandle3 = snapshotPool.SetSharedSnapshot();
      Assert.AreEqual(zeroSnapshot, modifiedHandle3);

      snapshotPool.SetSharedSnapshotUint32(1, valueToSet: 0);
      snapshotPool.SetSharedSnapshotUint32(3, valueToSet: 0);

      var modifiedHandle4 = snapshotPool.SetSharedSnapshot();
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

      var modifiedSnapshot4 = snapshotPool.SetSharedSnapshot();
      Assert.AreEqual(modifiedSnapshot, modifiedSnapshot4);
    }

    [Test]
    public void PoolResize()
    {
      var rows = (
          from a in Enumerable.Range(0, 10)
          from b in Enumerable.Range(0, 10)
          from c in Enumerable.Range(0, 10)
          from d in Enumerable.Range(0, 10)
          select new[] {(uint) a, (uint) b, (uint) c, (uint) d}
        ).ToList();

      var snapshotPool = new SnapshotPool(4 * sizeof(uint));
      var handles = new HashSet<SnapshotHandle>();
      var xs = new Dictionary<SnapshotHandle, uint[]>();

      SnapshotHandle aaaa;

      foreach (var row in rows)
      {
        for (var index = 0; index < row.Length; index++)
        {
          snapshotPool.SetSharedSnapshotUint32(index, row[index]);
        }

        

        var arr = snapshotPool.SnapshotToDebugArray(SnapshotPool.SharedSnapshot);
        if (arr.SequenceEqual(new uint[] {8, 4, 1, 9}))
        {
          //aaaa = modifiedSnapshot;
          GC.KeepAlive(this);
        }

        var modifiedSnapshot = snapshotPool.SetSharedSnapshot();
        Assert.IsTrue(handles.Add(modifiedSnapshot));

        xs.Add(modifiedSnapshot, arr);
      }


      Assert.That(rows.Count, Is.EqualTo(handles.Count));

      var snapshot = SnapshotPool.ZeroSnapshot;

      foreach (var row in rows)
      {
        for (var index = 0; index < row.Length; index++)
        {
          var snapshot1 = snapshotPool.SetSingleUInt32(snapshot, index, row[index]);

          

          if (!handles.Contains(snapshot1))
          {
            var a = snapshotPool.SnapshotToDebugArray(snapshot);
            var b = snapshotPool.SnapshotToDebugArray(snapshot1);

            GC.KeepAlive(this);
            Assert.Fail();
          }


          snapshot = snapshot1;
        }
      }

      GC.KeepAlive(this);

      //Assert.AreEqual(xs.Count(), 42);
    }
  }
}