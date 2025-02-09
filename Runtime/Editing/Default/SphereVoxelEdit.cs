using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[assembly: RegisterGenericJobType(typeof(jedjoud.VoxelTerrain.Edits.VoxelEditJob<jedjoud.VoxelTerrain.Edits.SphereVoxelEdit>))]
namespace jedjoud.VoxelTerrain.Edits {

    public struct SphereVoxelEdit : IVoxelEdit {
        [ReadOnly] public float3 center;
        [ReadOnly] public float radius;
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
                extents = new Vector3(radius, radius, radius),
            };
        }

        public Voxel Modify(float3 position, Voxel voxel) {
            float density = math.length(position - center) - radius;
            voxel.material = (density < 1.0F && writeMaterial) ? material : voxel.material;
            if (!paintOnly) {
                voxel.density = (density < 0.0F) ? (half)(density * strength) : voxel.density;
            }
            return voxel;
        }
    }
}