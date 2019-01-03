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
      var value = snapshotPool.GetUint32(SnapshotHandle.Zero, elementIndex: 1);
      Assert.AreEqual(value, 0);
      
      
    }
  }
}