using JetBrains.Annotations;

namespace MemorySnapshotPool.Storage
{
  public interface ISnapshotStorage
  {
    void Initialize(uint capacityInInts);
    
    uint MemoryConsumptionTotalInBytes { get; }

    [Pure] uint GetUint32(uint offset, uint index);
    
    [Pure] bool CompareRange(SnapshotHandle snapshot1, SnapshotHandle snapshot2, uint startIndex, uint endIndex);

    void Copy(SnapshotHandle sourceSnapshot, SnapshotHandle targetSnapshot, uint intsToCopy);
    void MutateUint32(SnapshotHandle snapshot, uint elementIndex, uint value);

    // todo: how to report overflows? or better check manually?
    [MustUseReturnValue] SnapshotHandle AllocateNewHandle(uint intsToAllocate);
  }
}