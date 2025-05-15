using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


[assembly: RegisterGenericJobType(typeof(jedjoud.VoxelTerrain.Edits.VoxelEditJob<jedjoud.VoxelTerrain.Edits.SetDensityVoxelEdit>))]
namespace jedjoud.VoxelTerrain.Edits {

    public struct SetDensityVoxelEdit : IVoxelEdit {
        [ReadOnly] public float3 center;
        [ReadOnly] public float targetDensity;
        [ReadOnly] public float radius;

        public JobHandle Apply(float3 offset, NativeArray<Voxel> voxels, NativeMultiCounter counters) {
            return IVoxelEdit.ApplyGeneric(this, offset, voxels, counters);
        }

        public Bounds GetBounds() {
            return new Bounds {
                center = center,
                extents = new Vector3(radius, radius, radius)
            };
        }

        public Voxel Modify(float3 position, Voxel voxel) {
            float density = math.length(position - center) - radius;
            float falloff = math.saturate(-(density / radius));
            voxel.density = (half)(math.lerp(voxel.density, targetDensity, falloff));
            return voxel;
        }
    }
}