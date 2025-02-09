using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Unity.Mathematics;


[assembly: RegisterGenericJobType(typeof(VoxelEditJob<FlatMapJob>))]
public struct FlatMapJob : IVoxelEdit {
    [ReadOnly] public float3 center;
    [ReadOnly] public float strength;
    [ReadOnly] public float radius;
    [ReadOnly] public byte material;
    [ReadOnly] public bool writeMaterial;
    [ReadOnly] public bool maskMaterial;
    [ReadOnly] public float falloffOffset;

    public JobHandle Apply(float3 offset, NativeArray<Voxel> voxels, NativeMultiCounter counters) {
        return IVoxelEdit.ApplyGeneric(this, offset, voxels, counters);
    }

    public Bounds GetBounds() {
        return new Bounds();
    }

    public Voxel Modify(float3 position, Voxel voxel) {
        float minHeight = -1f;
        float stoneHardness = (noise.snoise(position.xz * 0.01f) * 0.5f + 0.5f) * 10 + 2.0f;

        voxel.material = (byte)(position.y > minHeight ? 0 : 1);
        voxel.density = (half)(position.y * (position.y > minHeight ? 1 : stoneHardness));
        return voxel;
    }
}