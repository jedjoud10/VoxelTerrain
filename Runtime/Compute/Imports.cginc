// Size is actually 132, since it's double of 66!!! (octal stuff + normal padding + padding voxel)
// Unless we are running a preview, and in which case the size can be anything!!!
int size;

static const int PHYSICAL_SIZE = 32;
static const int LOGICAL_SIZE = PHYSICAL_SIZE + 2;

int3 permuationSeed;
int3 moduloSeed;

#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Props.cginc"
#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Noises.cginc"
#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/SDF.cginc"
#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Other.cginc"
#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Voxel.cginc"

#ifdef _ASYNC_READBACK_OCTAL
RWStructuredBuffer<uint> voxels;
#else
RWTexture3D<uint> voxels_write;
float3 simpleScale;
float3 simpleOffset;
#endif

//RWStructuredBuffer<BlittableProp> props;
//RWStructuredBuffer<int> props_counter;


#ifdef _ASYNC_READBACK_OCTAL
// why the FUCK does CBUFFER not work :sob: :skull:
RWStructuredBuffer<int> neg_pos_octal_counters;
StructuredBuffer<float4> pos_scale_octals;
#endif

#ifdef _ASYNC_READBACK_OCTAL
float3 ConvertIntoWorldPosition(uint3 id) {
        uint3 zero_to_three = id / LOGICAL_SIZE;
        int chunk_index = zero_to_three.x + zero_to_three.z * 4 + zero_to_three.y * 16;

        float4 pos_scale = pos_scale_octals[chunk_index];
        return (float3)((int3)(id % LOGICAL_SIZE) * pos_scale.w) + pos_scale.xyz;
}
#else
float3 ConvertIntoWorldPosition(uint3 id) {
    return ((float3)id * simpleScale) + simpleOffset;
}
#endif

#ifdef _ASYNC_READBACK_OCTAL
void CheckVoxelSign(uint3 id, float value) {
    uint3 zero_to_three = id / LOGICAL_SIZE;
    int chunk_index = zero_to_three.x + zero_to_three.z * 4 + zero_to_three.y * 16;
    InterlockedAdd(neg_pos_octal_counters[chunk_index], value >= 0.0 ? 1 : -1);
}
#endif

void StoreVoxel(uint3 id, float density, int material) {
    #ifdef _ASYNC_READBACK_OCTAL
        uint3 zero_to_three = id / LOGICAL_SIZE;
        int chunk_index = zero_to_three.x + zero_to_three.z * 4 + zero_to_three.y * 16;
        uint3 local_id = id % LOGICAL_SIZE;

        int index = local_id.x + local_id.z * LOGICAL_SIZE + local_id.y * LOGICAL_SIZE*LOGICAL_SIZE + chunk_index * LOGICAL_SIZE*LOGICAL_SIZE*LOGICAL_SIZE;

        // this is a buffer!!!
        // contiguous buffer containing 64 chunks worth of data
        voxels[index] = packVoxelData(density, material);
        CheckVoxelSign(id, density);
    #else
        voxels_write[id] = packVoxelData(density, material);
    #endif
}