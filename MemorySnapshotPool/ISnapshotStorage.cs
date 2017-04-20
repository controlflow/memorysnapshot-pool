using JetBrains.Annotations;

namespace MemorySnapshotPool
{
  public interface ISnapshotStorage
  {
    int MemoryConsumptionTotalInBytes { get; }

    [Pure] uint GetUint32(SnapshotHandle snapshot, int elementIndex);
    [Pure] bool CompareRange(SnapshotHandle snapshot1, SnapshotHandle snapshot2, int startIndex, int endIndex);

    void Copy(SnapshotHandle sourceSnapshot, SnapshotHandle targetSnapshot);
    void MutateUint32(SnapshotHandle snapshot, int elementIndex, uint value);

    [MustUseReturnValue] SnapshotHandle AllocNewHandle();
  }
}