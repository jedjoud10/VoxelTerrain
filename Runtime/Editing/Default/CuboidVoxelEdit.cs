using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
[assembly: RegisterGenericJobType(typeof(jedjoud.VoxelTerrain.Edits.VoxelEditJob<jedjoud.VoxelTerrain.Edits.CuboidVoxelEdit>))]
namespace jedjoud.VoxelTerrain.Edits {
    public struct CuboidVoxelEdit : IVoxelEdit {
        [ReadOnly] public float3 center;
        [ReadOnly] public float3 halfExtents;
        [ReadOnly] public float strength;
        [ReadOnly] public byte material;
        [ReadOnly] public bool writeMaterial;
        [ReadOnly] public bool paintOnly;

        public JobHandle Apply(float3 offset, NativeArray<Voxel> voxels, Unsafe.NativeMultiCounter counters) {
            return IVoxelEdit.ApplyGeneric(this, offset, voxels, counters);
        }

        public Bounds GetBounds() {
            return new Bounds {
                center = center,
                extents = halfExtents
            };
        }

        public Voxel Modify(float3 position, Voxel voxel) {
            float3 q = math.abs(position - center) - halfExtents;
            float density = math.length(math.max(q, 0.0F)) + math.min(math.max(q.x, math.max(q.y, q.z)), 0.0F);

            voxel.material = (density < 1.0F && writeMaterial) ? material : voxel.material;
            if (!paintOnly) {
                voxel.density = (density < 0.0F) ? (half)(density * strength) : voxel.density;
            }
            return voxel;
        }
    }
}