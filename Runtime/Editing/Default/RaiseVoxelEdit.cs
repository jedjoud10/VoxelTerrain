using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[assembly: RegisterGenericJobType(typeof(jedjoud.VoxelTerrain.Edits.VoxelEditJob<jedjoud.VoxelTerrain.Edits.RaiseVoxelEdit>))]
namespace jedjoud.VoxelTerrain.Edits {

    // Will either raise / lower the terrain
    public struct RaiseVoxelEdit : IVoxelEdit {
        [ReadOnly] public float3 center;
        [ReadOnly] public float strength;
        [ReadOnly] public float radius;
        [ReadOnly] public byte material;
        [ReadOnly] public bool writeMaterial;

        public JobHandle Apply(float3 offset, NativeArray<Voxel> voxels, Unsafe.NativeMultiCounter counters) {
            return IVoxelEdit.ApplyGeneric(this, offset, voxels, counters);
        }

        public Bounds GetBounds() {
            return new Bounds {
                center = center,
                extents = new Vector3(radius, radius * 2000.0f, radius)
            };
        }

        public Voxel Modify(float3 position, Voxel voxel) {
            float density = math.length(position.xz - center.xz) - radius;
            voxel.material = (density < 1.0F && writeMaterial && strength < 0) ? material : voxel.material;

            float falloff = math.saturate(-(density / radius));
            voxel.density += (half)(strength * falloff);
            return voxel;
        }
    }
}