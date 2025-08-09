struct Voxel {
    float density;
    uint material;
};

typedef uint2 PackedVoxel;

Voxel unpackVoxelData(PackedVoxel packed) {
    Voxel voxel;
    voxel.density = f16tof32(packed.x & 0xFFFF);
    voxel.material = (packed.y >> 16) & 0xFF; 
    return voxel;
}

PackedVoxel packVoxelData(Voxel voxel) {
    uint first = f32tof16(voxel.density) | (clamp(voxel.material, 0, 255) << 16);

    return uint2(first, 0);
}