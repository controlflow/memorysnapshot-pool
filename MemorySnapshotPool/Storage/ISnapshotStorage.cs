using JetBrains.Annotations;

namespace MemorySnapshotPool
{
  public interface ISnapshotStorage
  {
    uint MemoryConsumptionTotalInBytes { get; }

    [Pure] uint GetUint32(SnapshotHandle snapshot, uint elementIndex);
    [Pure] bool CompareRange(SnapshotHandle snapshot1, SnapshotHandle snapshot2, uint startIndex, uint endIndex);

    void Copy(SnapshotHandle sourceSnapshot, SnapshotHandle targetSnapshot);
    void MutateUint32(SnapshotHandle snapshot, uint elementIndex, uint value);

    // todo: how to report overflows? or better check manually?
    [MustUseReturnValue] SnapshotHandle AllocateNewHandle();
  }
}