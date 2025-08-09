struct Voxel {
    float density;
    uint material;
    float4 layers;
};

typedef uint2 PackedVoxel;

uint PackUnorm8(float4 value) {
    uint4 bytes = uint4(round(saturate(value) * 255.0));
    return (bytes.x) | (bytes.y << 8) | (bytes.z << 16) | (bytes.w << 24);
}

float4 UnpackUnorm8(uint val) {
    return float4(val & 0xFF, (val >> 8) & 0xFF, (val >> 16) & 0xFF, (val >> 24) & 0xFF) / 255.0;
}

Voxel unpackVoxelData(PackedVoxel packed) {
    Voxel voxel;
    voxel.density = f16tof32(packed.x & 0xFFFF);
    voxel.material = 0; 
    voxel.layers = UnpackUnorm8(packed.y);
    return voxel;
}

PackedVoxel packVoxelData(Voxel voxel) {
    uint first = f32tof16(voxel.density) | (clamp(voxel.material, 0, 255) << 16);
    uint second = PackUnorm8(voxel.layers);
    return uint2(first, second);
}