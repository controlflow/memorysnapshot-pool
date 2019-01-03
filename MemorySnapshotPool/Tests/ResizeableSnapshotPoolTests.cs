using MemorySnapshotPool.View;
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
      
      // todo: append more
    }

    [Test]
    public void AppendWithMask()
    {
      var snapshotPool = new SnapshotPool(bytesPerSnapshot: null);
      var emptySnapshot = SnapshotHandle.Zero;

      var snapshot1 = snapshotPool.AppendBytesToSnapshot(
        emptySnapshot, bytesToAdd: 2, maskToBeAppliedToFirstOfAppendedUint32Elements: 0x00A0CDEF);
      var snapshot2 = snapshotPool.AppendBytesToSnapshot(
        snapshot1, bytesToAdd: 1, 0x000B0000);

      var snapshot3 = snapshotPool.AppendBytesToSnapshot(
        emptySnapshot, bytesToAdd: 3, maskToBeAppliedToFirstOfAppendedUint32Elements: 0x00ABCDEF);

      Assert.AreEqual(snapshot2, snapshot3);
      Assert.AreEqual(0x00ABCDEF, snapshotPool.GetUint32(snapshot3, elementIndex: 0));
    }

    [Test]
    public void ByteAppend()
    {
      var snapshotPool = new SnapshotPoolByteView(new SnapshotPool());
      Assert.AreEqual(new byte[0], snapshotPool.SnapshotToDebugArray(SnapshotHandle.Zero));

      var snapshot1 = snapshotPool.AppendSingleByte(SnapshotHandle.Zero, valueToAdd: 12);
      var snapshot2 = snapshotPool.AppendSingleByte(SnapshotHandle.Zero, valueToAdd: 12);
      Assert.AreEqual(snapshot1, snapshot2);
      Assert.AreEqual(new byte[] { 12 }, snapshotPool.SnapshotToDebugArray(snapshot1));

      var snapshot3 = snapshotPool.AppendSingleByte(snapshot1, valueToAdd: 13);
      Assert.AreEqual(new byte[] { 12, 13 }, snapshotPool.SnapshotToDebugArray(snapshot3));
      
      var snapshot4 = snapshotPool.AppendSingleByte(snapshot3, valueToAdd: 42);
      Assert.AreEqual(new byte[] { 12, 13, 42 }, snapshotPool.SnapshotToDebugArray(snapshot4));
    }
  }
}