// Size is actually 132, since it's double of 66!!! (octal stuff + padding voxel)
// Unless we are running a preview, and in which case the size can be anything!!!
int size;

int3 permuationSeed;
int3 moduloSeed;

float3 scale;
float3 offset;

#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Props.cginc"
#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Noises.cginc"
#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/SDF.cginc"
#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Other.cginc"
#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Voxel.cginc"

#ifdef _ASYNC_READBACK_OCTAL
RWStructuredBuffer<uint> voxels;
#else
RWTexture3D<uint> voxels_write;
#endif

//RWStructuredBuffer<BlittableProp> props;
//RWStructuredBuffer<int> props_counter;


#ifdef _ASYNC_READBACK_OCTAL
// why the FUCK does CBUFFER not work :sob: :skull:
RWStructuredBuffer<int> neg_pos_octal_counters;
StructuredBuffer<float4> pos_scale_octals;
#endif

float3 ConvertIntoWorldPosition(uint3 id) {
    #ifdef _ASYNC_READBACK_OCTAL
        uint3 zero_to_one = id / 66;
        int chunk_index = zero_to_one.x + zero_to_one.z * 2 + zero_to_one.y * 4;

        float4 pos_scale = pos_scale_octals[chunk_index];
        return ((float3)id % 66) * pos_scale.w + pos_scale.xyz;
    #else
        return ((float3)id * scale) + offset;
    #endif
}

/*
float3 ConvertFromWorldPosition(float3 worldPos) {
    return  (worldPos / scale) - offset;
}
*/

int CalcIdIndex(uint3 id) {
    #ifdef _ASYNC_READBACK_OCTAL
        uint3 zero_to_one = id / 66;
        int chunk_index = zero_to_one.x + zero_to_one.z * 2 + zero_to_one.y * 4;
        uint3 local_id = id % 66;

        return local_id.x + local_id.z * 66 + local_id.y * 66*66 + chunk_index * 66*66*66;
    #else
        return id.x + id.z * size + id.y * size * size;
    #endif
}

#ifdef _ASYNC_READBACK_OCTAL
void CheckVoxelSign(uint3 id, float value) {
    uint3 zero_to_one = id / 66;
    int chunk_index = zero_to_one.x + zero_to_one.z * 2 + zero_to_one.y * 4;
    InterlockedAdd(neg_pos_octal_counters[chunk_index], value >= 0.0 ? 1 : -1);
}
#endif