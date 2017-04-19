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
      var snapshotPool = new SnapshotPool(bytesPerSnapshot: 10 * sizeof(uint));
      var zeroSnapshot = snapshotPool.ZeroSnapshot;

      var zeroArray = snapshotPool.ReadToSharedSnapshotArray(zeroSnapshot);
      Assert.That(zeroArray, Is.All.Zero);
      Assert.AreEqual(zeroArray.Length, ElementsPerSnapshot);

      for (var index = 0; index < ElementsPerSnapshot; index++)
      {
        Assert.That(snapshotPool.GetUint32(zeroSnapshot, index), Is.Zero);
      }

      var modifiedHandle = snapshotPool.SetUInt32(zeroSnapshot, 0, valueToSet: 0);
      Assert.AreEqual(zeroSnapshot, modifiedHandle);

      var modifiedHandle2 = snapshotPool.SetUInt32(zeroSnapshot, 9, valueToSet: 0);
      Assert.AreEqual(zeroSnapshot, modifiedHandle2);
    }

    [Test]
    public void ZeroBytes()
    {
      var snapshotPool = new SnapshotPool(bytesPerSnapshot: 0);
      var zeroSnapshot = snapshotPool.ZeroSnapshot;

      Assert.That(snapshotPool.ReadToSharedSnapshotArray(zeroSnapshot), Is.Empty);
    }

    [Test]
    public void Intern()
    {
      var snapshotPool = new SnapshotPool(bytesPerSnapshot: 10 * sizeof(uint));
      var zeroSnapshot = snapshotPool.ZeroSnapshot;

      var modifiedSnapshot = snapshotPool.SetUInt32(zeroSnapshot, elementIndex: 5, valueToSet: 42);
      Assert.AreNotEqual(zeroSnapshot, modifiedSnapshot);

      var modifiedArray = snapshotPool.ReadToSharedSnapshotArray(modifiedSnapshot);
      Assert.AreEqual(modifiedArray[5], 42);

      var modifiedSnapshot2 = snapshotPool.SetUInt32(zeroSnapshot, elementIndex: 5, valueToSet: 42);
      Assert.AreEqual(modifiedSnapshot, modifiedSnapshot2);

      var modifiedSnapshot3 = snapshotPool.SetUInt32(modifiedSnapshot2, elementIndex: 5, valueToSet: 0);
      Assert.AreEqual(zeroSnapshot, modifiedSnapshot3);
    }
  }
}