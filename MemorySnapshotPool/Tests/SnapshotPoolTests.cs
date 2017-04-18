using NUnit.Framework;

namespace MemorySnapshotPool
{
  [TestFixture]
  public class SnapshotPoolTests
  {
    const int BytesPerSnapshot = 10;

    [Test]
    public void ZeroModifications()
    {
      var snapshotPool = new SnapshotPool(bytesPerSnapshot: BytesPerSnapshot);
      var zeroSnapshot = snapshotPool.ZeroSnapshot;

      var zeroArray = snapshotPool.ReadToSharedSnapshotArray(zeroSnapshot);
      Assert.That(zeroArray, Is.All.Zero);
      Assert.AreEqual(zeroArray.Length, BytesPerSnapshot);

      for (var index = 0; index < BytesPerSnapshot; index++)
      {
        Assert.That(snapshotPool.GetElementValue(zeroSnapshot, index), Is.Zero);
      }

      var modifiedHandle = snapshotPool.SetElementValue(zeroSnapshot, 0, valueToSet: 0);
      Assert.AreEqual(zeroSnapshot, modifiedHandle);

      var modifiedHandle2 = snapshotPool.SetElementValue(zeroSnapshot, 9, valueToSet: 0);
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
      var snapshotPool = new SnapshotPool(bytesPerSnapshot: BytesPerSnapshot);
      var zeroSnapshot = snapshotPool.ZeroSnapshot;

      var modifiedSnapshot = snapshotPool.SetElementValue(zeroSnapshot, elementIndex: 5, valueToSet: 42);
      Assert.AreNotEqual(zeroSnapshot, modifiedSnapshot);

      var modifiedArray = snapshotPool.ReadToSharedSnapshotArray(modifiedSnapshot);
      Assert.AreEqual(modifiedArray[5], 42);

      var modifiedSnapshot2 = snapshotPool.SetElementValue(zeroSnapshot, elementIndex: 5, valueToSet: 42);
      Assert.AreEqual(modifiedSnapshot, modifiedSnapshot2);

      var modifiedSnapshot3 = snapshotPool.SetElementValue(modifiedSnapshot2, elementIndex: 5, valueToSet: 0);
      Assert.AreEqual(zeroSnapshot, modifiedSnapshot3);
    }
  }
}