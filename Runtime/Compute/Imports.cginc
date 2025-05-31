int3 permutation_seed;
int3 modulo_seed;
SamplerState my_linear_clamp_sampler;

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
#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Rotations.cginc"
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
    RWTexture3D<float> densities_texture_write;
    float3 segment_scale;
    float3 segment_offset;
#elif defined(_SEGMENT_PROPS)
    void Voxels(float3 position, out float density, out int material);
    int physical_segment_size;
    int segment_size;
    int segment_size_padded;
    Texture3D<float> densities_texture_read;
    float3 segment_scale;
    float3 segment_offset;

    int max_combined_temp_props;
    int max_total_prop_types;
    RWStructuredBuffer<uint> temp_counters_buffer;
    RWStructuredBuffer<uint> temp_buffer_offsets_buffer;
    RWStructuredBuffer<uint4> temp_buffer;
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


#if defined(_SEGMENT_PROPS)
    float DensityAtButVeryVerySlowButMuchHigherQuality(float3 position) {
        float density = 0;
        int mat = 0;
        Voxels(position, density, mat);
        return density;
    }

    float DensityAt(float3 position) {
        float3 zero_to_size = (position - segment_offset) / segment_scale;
        float3 zero_to_one = zero_to_size / (float)segment_size_padded;
        return densities_texture_read.SampleLevel(my_linear_clamp_sampler, zero_to_one, 0);
    }
    
    // I am torturing/abusing my gpu
    float3 CalculateFiniteDiffedNormalsSlow(float3 position) {
        const float EPSILON = 0.01;
        float base = DensityAtButVeryVerySlowButMuchHigherQuality(position);
        float x = DensityAtButVeryVerySlowButMuchHigherQuality(position + float3(EPSILON, 0, 0));
        float y = DensityAtButVeryVerySlowButMuchHigherQuality(position + float3(0, EPSILON, 0));
        float z = DensityAtButVeryVerySlowButMuchHigherQuality(position + float3(0, 0, EPSILON));
        return normalize(float3(x - base, y - base, z - base));
    }

    float3 CalculateFiniteDiffedNormals(float3 position) {
        const float EPSILON = 1;
        float base = DensityAt(position);
        float x = DensityAt(position + float3(EPSILON, 0, 0));
        float y = DensityAt(position + float3(0, EPSILON, 0));
        float z = DensityAt(position + float3(0, 0, EPSILON));
        return normalize(float3(x - base, y - base, z - base));
    }

    bool CheckSurfaceAlongAxis(float3 position, int axis, out float3 hitPosition, out float3 hitNormal) {
        const float3 OFFSETS[3] = {
            float3(1,0,0), float3(0,1,0), float3(0,0,1),
        };

        const int MAX_ITERATIONS = 8;
        const float PRECISION = 0.0001;
        const float OFFSET_SCALE = 8;

        hitPosition = 0;
        hitNormal = 0;
        
        float3 offsetAxis = OFFSETS[axis] * OFFSET_SCALE;
        float3 basePosition = position;
        float3 otherPosition = position + offsetAxis;
        float baseDensity = DensityAtButVeryVerySlowButMuchHigherQuality(basePosition);
        float otherDensity = DensityAtButVeryVerySlowButMuchHigherQuality(otherPosition);
        
        // Early out if no crossing
        if ((baseDensity >= 0.0) == (otherDensity >= 0.0)) {
            return false;
        }

        float3 lowPos = basePosition;
        float3 highPos = otherPosition;
        float lowDensity = baseDensity;
        float highDensity = otherDensity;
        float3 bestPos = position;
        
        // Perform binary search
        for (int i = 0; i < MAX_ITERATIONS; i++) {
            float3 midPos = (lowPos + highPos) * 0.5;
            float midDensity = DensityAtButVeryVerySlowButMuchHigherQuality(midPos);
            
            if (abs(midDensity) < PRECISION) {
                bestPos = midPos;
                break;
            }
            
            if ((midDensity >= 0.0) != (lowDensity >= 0.0)) {
                highPos = midPos;
                highDensity = midDensity;
            } else {
                lowPos = midPos;
                lowDensity = midDensity;
            }
            
            bestPos = midPos;
        }

        hitNormal = CalculateFiniteDiffedNormalsSlow(bestPos);
        //hitNormal = CalculateFiniteDiffedNormals(bestPos);
        hitPosition = bestPos;
        return true;
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
        #if defined(_ASYNC_READBACK_OCTAL)
            uint3 zero_to_three = id / LOGICAL_SIZE;
            int chunk_index = zero_to_three.x + zero_to_three.z * 4 + zero_to_three.y * 16;
            uint3 local_id = id % LOGICAL_SIZE;
    
            int index = local_id.x + local_id.z * LOGICAL_SIZE + local_id.y * LOGICAL_SIZE*LOGICAL_SIZE + chunk_index * LOGICAL_SIZE*LOGICAL_SIZE*LOGICAL_SIZE;
    
            // this is a buffer!!!
            // contiguous buffer containing 64 chunks worth of data
            voxels_buffer[index] = packVoxelData(density, material);
            CheckVoxelSign(id, density);
        #elif defined(_PREVIEW)
            voxels_texture_write[id] = packVoxelData(density, material);
        #elif defined(_SEGMENT_VOXELS)
            densities_texture_write[id] = density;
        #endif
    }
#endif