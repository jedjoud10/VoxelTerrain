int3 permutation_seed = int3(684, 2325, 31);
int3 modulo_seed = int3(423, 4543, -23423);

#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Voxel.cginc"
#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Noises/common.cginc"

float fetch(uint3 id) {
    float density;
    uint material;
    unpackVoxelData(voxels[id], density, material);
    return density;
}

float3 fetchMatId(uint3 id) {
    float density;
    uint material;
    unpackVoxelData(voxels[id], density, material);
    return hash31((float)material * 43.2342321 + 543.3232);
}