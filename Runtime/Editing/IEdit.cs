using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

namespace jedjoud.VoxelTerrain.Edits {
    public interface IEdit {
        public void Modify(float3 position, ref float density);
        public MinMaxAABB GetBounds();

        //public JobHandle Apply(float3 offset, NativeArray<Voxel> voxels);

        /*
        public static JobHandle ApplyGeneric<T>(T edit, float3 offset, NativeArray<Voxel> voxels) where T : struct, IVoxelEdit {
            VoxelEditJob<T> job = new VoxelEditJob<T> {
                offset = offset,
                edit = edit,
                voxels = voxels,
                voxelScale = VoxelTerrain.Instance.voxelSizeFactor,
                counters = counters,
            };
            return job.Schedule(65*65*65, 4096);
        }
        */
    }
}