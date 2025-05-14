void unpackVoxelData(uint packed, out float density, out uint material) {
    density = f16tof32(packed & 0xFFFF);
    material = packed >> 16;
}

float unpackDensity(uint packed) {
    return f16tof32(packed & 0xFFFF);
}

uint packVoxelData(float density, uint material) {
    return f32tof16(density) | (material << 16);
}