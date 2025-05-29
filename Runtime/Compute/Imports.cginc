int3 permutation_seed;
int3 modulo_seed;

#if defined(_ASYNC_READBACK_OCTAL)
    static const int PHYSICAL_SIZE = 32;
    static const int LOGICAL_SIZE = PHYSICAL_SIZE + 2;
#endif


#pragma warning (disable : 3571)
#pragma warning (disable : 3078)

// I should probably actually care why it complains about temp register shit
// but I will not. fuck it we ball
#pragma warning (disable : 4714)


#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Props.cginc"
#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Noises.cginc"
#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/SDF.cginc"
#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Other.cginc"
#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Voxel.cginc"

#if defined(_ASYNC_READBACK_OCTAL)
    // why the FUCK does CBUFFER not work :sob: :skull:
    RWStructuredBuffer<int> neg_pos_octal_counters;
    StructuredBuffer<float4> pos_scale_octals;

    int size;
    RWStructuredBuffer<uint> voxels_buffer;
#elif defined(_PREVIEW)
    int size;
    RWTexture3D<uint> voxels_texture_write;
    float3 preview_scale;
    float3 preview_offset;
#elif defined(_SEGMENT_VOXELS)
    int size;
    RWTexture3D<uint> voxels_texture_write;
    float3 segment_scale;
    float3 segment_offset;
#elif defined(_SEGMENT_PROPS)
    int physical_segment_size;
    int segment_size;
    Texture3D<uint> voxels_texture_read;
    float3 segment_scale;
    float3 segment_offset;

    int max_combined_temp_props;
    RWStructuredBuffer<uint> temp_counters_buffer;
    RWStructuredBuffer<uint> temp_buffer_offsets_buffer;
    RWStructuredBuffer<uint2> temp_buffer;
#endif

#if defined(_ASYNC_READBACK_OCTAL)
    float3 ConvertIntoWorldPosition(uint3 id) {
            uint3 zero_to_three = id / LOGICAL_SIZE;
            int chunk_index = zero_to_three.x + zero_to_three.z * 4 + zero_to_three.y * 16;
    
            float4 pos_scale = pos_scale_octals[chunk_index];
            return (float3)((int3)(id % LOGICAL_SIZE) * pos_scale.w) + pos_scale.xyz;
    }
#elif defined(_PREVIEW)
    float3 ConvertIntoWorldPosition(uint3 id) {
        return ((float3)id * preview_scale) + preview_offset;
    }
#elif defined(_SEGMENT_VOXELS)
    float3 ConvertIntoWorldPosition(uint3 id) {
        return ((float3)id * segment_scale) + segment_offset;
    }
#elif defined(_SEGMENT_PROPS)
    float3 ConvertIntoWorldPosition(uint3 id) {
        return hash31((float)id.x) * (float)physical_segment_size + segment_offset;
    }
#endif

#if defined(_ASYNC_READBACK_OCTAL)
    void CheckVoxelSign(uint3 id, float value) {
        uint3 zero_to_three = id / LOGICAL_SIZE;
        int chunk_index = zero_to_three.x + zero_to_three.z * 4 + zero_to_three.y * 16;
        InterlockedAdd(neg_pos_octal_counters[chunk_index], value >= 0.0 ? 1 : -1);
    }
#endif

#if defined(_ASYNC_READBACK_OCTAL) || defined(_SEGMENT_VOXELS) || defined(_PREVIEW)
    void StoreVoxel(uint3 id, float density, int material) {
        #ifdef _ASYNC_READBACK_OCTAL
            uint3 zero_to_three = id / LOGICAL_SIZE;
            int chunk_index = zero_to_three.x + zero_to_three.z * 4 + zero_to_three.y * 16;
            uint3 local_id = id % LOGICAL_SIZE;
    
            int index = local_id.x + local_id.z * LOGICAL_SIZE + local_id.y * LOGICAL_SIZE*LOGICAL_SIZE + chunk_index * LOGICAL_SIZE*LOGICAL_SIZE*LOGICAL_SIZE;
    
            // this is a buffer!!!
            // contiguous buffer containing 64 chunks worth of data
            voxels_buffer[index] = packVoxelData(density, material);
            CheckVoxelSign(id, density);
        #else
            voxels_texture_write[id] = packVoxelData(density, material);
        #endif
    }
#endif