using NUnit.Framework;

namespace MemorySnapshotPool.Tests
{
  [TestFixture]
  public class ResizeableSnapshotPoolTests
  {
    [Test]
    public void Simple()
    {
      var snapshotPool = new SnapshotPool(bytesPerSnapshot: null);
      var emptySnapshot = SnapshotHandle.Zero;
      Assert.AreEqual(emptySnapshot.SnapshotSizeInBytes, 0);

      var oneByteSnapshot = snapshotPool.AppendBytesToSnapshot(emptySnapshot, bytesToAdd: 1);
      var value = snapshotPool.GetUint32(oneByteSnapshot, elementIndex: 0);
      Assert.AreEqual(value, 0);
      Assert.AreNotEqual(emptySnapshot, oneByteSnapshot);
      Assert.AreEqual(oneByteSnapshot.SnapshotSizeInBytes, 1);

      var otherOneByte = snapshotPool.AppendBytesToSnapshot(emptySnapshot, bytesToAdd: 1);
      Assert.AreEqual(otherOneByte, oneByteSnapshot);
      
      var twoByteSnapshot = snapshotPool.AppendBytesToSnapshot(emptySnapshot, bytesToAdd: 2);
      Assert.AreEqual(twoByteSnapshot.SnapshotSizeInBytes, 2);
      Assert.AreNotEqual(emptySnapshot, twoByteSnapshot);
      Assert.AreNotEqual(oneByteSnapshot, twoByteSnapshot);
      Assert.AreEqual(twoByteSnapshot, snapshotPool.AppendBytesToSnapshot(oneByteSnapshot, bytesToAdd: 1));

      var threeByteSnapshot = snapshotPool.AppendBytesToSnapshot(emptySnapshot, bytesToAdd: 3);
      Assert.AreEqual(threeByteSnapshot.SnapshotSizeInBytes, 3);
      Assert.AreEqual(threeByteSnapshot, snapshotPool.AppendBytesToSnapshot(twoByteSnapshot, bytesToAdd: 1));

      var modifiedSnapshot = snapshotPool.SetSingleUInt32(threeByteSnapshot, elementIndex: 0, valueToSet: 0xABCDEF12);
      Assert.AreNotEqual(modifiedSnapshot, threeByteSnapshot);
      Assert.AreEqual(0xCDEF12, snapshotPool.GetUint32(modifiedSnapshot, elementIndex: 0));
    }
  }
}