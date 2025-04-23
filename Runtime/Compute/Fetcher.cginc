#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Voxel.cginc"

float fetch(uint3 id) {
    float density;
    uint material;
    unpackVoxelData(voxels[id], density, material);
    return density;
}

float3 matId(uint3 id) {
    float density;
    uint material;
    unpackVoxelData(voxels[id], density, material);
    return density;
}