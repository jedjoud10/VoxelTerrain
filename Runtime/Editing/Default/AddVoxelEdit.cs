using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[assembly: RegisterGenericJobType(typeof(jedjoud.VoxelTerrain.Edits.VoxelEditJob<jedjoud.VoxelTerrain.Edits.AddVoxelEdit>))]
namespace jedjoud.VoxelTerrain.Edits {
    // Will either add / remove matter from the terrain
    public struct AddVoxelEdit : IVoxelEdit {
        [ReadOnly] public float3 center;
        [ReadOnly] public float strength;
        [ReadOnly] public float radius;
        [ReadOnly] public byte material;
        [ReadOnly] public bool writeMaterial;
        [ReadOnly] public bool maskMaterial;
        [ReadOnly] public float falloffOffset;
        [ReadOnly] public float3 scale;

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
            float density = math.length((position - center) * scale) - radius;
            float falloff = (maskMaterial && voxel.material != material) ? 0f : math.saturate(-(density / radius) + falloffOffset);

            voxel.material = (density < 1.0F && writeMaterial && !maskMaterial && strength < 0) ? material : voxel.material;
            voxel.density += (half)(strength * falloff);
            return voxel;
        }
    }
}