using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[assembly: RegisterGenericJobType(typeof(VoxelEditJob<RaiseVoxelEdit>))]

public struct FlatTerrainVoxelEdit : IVoxelEdit {
    [ReadOnly] public float3 center;
    [ReadOnly] public float strength;
    [ReadOnly] public float radius;
    [ReadOnly] public byte material;
    [ReadOnly] public bool writeMaterial;

    public JobHandle Apply(float3 offset, NativeArray<Voxel> voxels, NativeMultiCounter counters) {
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

        float minHeight = -1f;
        float stoneHardness = (noise.snoise(position.xz * 0.01f) * 0.5f + 0.5f) * 10 + 2.0f;

        voxel.material = (byte)(position.y > minHeight ? 0 : 1);
        voxel.density = (half)(position.y * (position.y > minHeight ? 1 : stoneHardness));

        return voxel;
    }
}