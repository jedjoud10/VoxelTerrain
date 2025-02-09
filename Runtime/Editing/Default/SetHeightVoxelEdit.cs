using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[assembly: RegisterGenericJobType(typeof(jedjoud.VoxelTerrain.Edits.VoxelEditJob<jedjoud.VoxelTerrain.Edits.SetHeightVoxelEdit>))]
namespace jedjoud.VoxelTerrain.Edits {

    public struct SetHeightVoxelEdit : IVoxelEdit {
        [ReadOnly] public float3 center;
        [ReadOnly] public float targetHeight;
        [ReadOnly] public float radius;
        [ReadOnly] public float strength;

        public JobHandle Apply(float3 offset, NativeArray<Voxel> voxels, Unsafe.NativeMultiCounter counters) {
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
            float falloff = math.saturate(-(density / radius) * strength);
            voxel.density = (half)(math.lerp(voxel.density, position.y - targetHeight, falloff));
            return voxel;
        }
    }
}