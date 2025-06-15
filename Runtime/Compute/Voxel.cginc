struct Voxel {
    float density;
    uint material;
};

Voxel unpackVoxelData(uint packed) {
    Voxel voxel;
    voxel.density = f16tof32(packed & 0xFFFF);
    voxel.material = (packed >> 16) & 0xFF; 
    return voxel;
}

uint packVoxelData(Voxel voxel) {
    uint first = f32tof16(voxel.density) | (clamp(voxel.material, 0, 255) << 16);
    return first;
}