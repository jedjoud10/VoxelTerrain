struct Voxel {
    float density;
    uint material;
};

struct PackedVoxel {
    uint first;
    uint second;
};

Voxel unpackVoxelData(PackedVoxel packed) {
    Voxel voxel;
    voxel.density = f16tof32(packed.first & 0xFFFF);
    voxel.material = (packed.first >> 16) & 0xFF; 
    return voxel;
}

PackedVoxel packVoxelData(Voxel voxel) {
    uint first = f32tof16(voxel.density) | (clamp(voxel.material, 0, 255) << 16);

    PackedVoxel packed;
    packed.first = first;
    packed.second = 0;
    return packed;
}