using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

namespace jedjoud.VoxelTerrain.Meshing {
    [BurstCompile(CompileSynchronously = true)]
    public struct MaterialJob : IJobParallelFor {
        [ReadOnly]
        public NativeArray<Voxel> voxels;

        [ReadOnly]
        public UnsafePtrList<Voxel> neighbours;

        [ReadOnly]
        public bool3 neighbourMask;

        public NativeParallelHashSet<byte>.ParallelWriter materialHashSet;
        public NativeParallelHashMap<byte, int>.ParallelWriter materialHashMap;
        public Unsafe.NativeCounter.Concurrent materialCounter;

        public void Execute(int index) {
            uint3 position = VoxelUtils.IndexToPos(index, VoxelUtils.SIZE + 1);

            if (!VoxelUtils.CheckNeighbours(position, neighbourMask))
                return;

            Voxel voxel = VoxelUtils.FetchWithNeighbours(VoxelUtils.PosToIndexMorton(position), ref voxels, ref neighbours);
            if (materialHashSet.Add(voxel.material)) {
                materialHashMap.TryAdd(voxel.material, materialCounter.Increment());
            }
        }
    }
}