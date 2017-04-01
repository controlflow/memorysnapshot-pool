using System;
using System.Diagnostics;

namespace MemorySnapshotPool
{
  public class Program
  {
    private static void Main(string[] args)
    {
      var spanshotPool = new SnapshotPool(10);

      var initial = spanshotPool.ZeroSnapshot;

      var array = spanshotPool.ReadToSharedSnapshotArray(initial);

      var modified = spanshotPool.SetElementValue(initial, elementIndex: 5, valueToSet: 42);
      var modified2 = spanshotPool.SetElementValue(initial, elementIndex: 5, valueToSet: 42);

      Debug.Assert(modified == modified2);

      var array2 = spanshotPool.ReadToSharedSnapshotArray(modified);
    }
  }
}
