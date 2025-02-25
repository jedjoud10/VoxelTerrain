// NOTE: For some fucking reason whenever we read the data on the CPU
// endianess / byte order is fucked and the lowest byte is actually the highest idk
// Will need to figure out why this happens later on but for now it works ok

struct BlittableProp {
    uint2 packed_position_and_scale;
    uint2 packed_rotation_dispatch_index_prop_variant_padding;
};

struct Prop {
    float3 position;
	float3 rotation;
    float scale;
};

uint2 PackPositionAndScale(float3 position, float scale) {
	uint x = f32tof16(position.x);
	uint y = f32tof16(position.y);
	uint z = f32tof16(position.z);
	uint w = f32tof16(scale);

	uint first = x | (y << 16);
	uint second = z | (w << 16);
	return uint2(first, second);
}

uint NormalizeAndPackAngle(float angle) {
	if (angle < 0) {
		angle = fmod(360 + angle, 360.0);
	}

	angle = fmod(angle, 360.0);
	return (uint)((angle / 360.0) * 255.0);
}

float UnpackRotation(uint rot) {
	return (rot / 255.0) * 360.0;
}

uint2 PackRotationAndVariantAndId(float3 rotation, uint propVariant, uint id) {
	uint x = NormalizeAndPackAngle(rotation.x);
	uint y = NormalizeAndPackAngle(rotation.y);
	uint z = NormalizeAndPackAngle(rotation.z);
	uint rots = x | (y << 8) | (z << 16);
	uint rest = id | (propVariant << 16);

	return uint2(rots, rest);
}

float3 UnpackRotation(uint2 packed) {
	return float3(UnpackRotation(packed.x >> 16), UnpackRotation(packed.x >> 8), UnpackRotation(packed.x & 0xFF));
}

float4 UnpackPositionAndScale(uint2 packed) {
	float x = f16tof32(packed.x & 0xFFFF);
	float y = f16tof32(packed.x >> 16);
	float z = f16tof32(packed.y & 0xFFFF);
	float w = f16tof32(packed.y >> 16);
	return float4(x, y, z, w);
}

uint UnpackVariant(uint2 packed) {
	return packed.y >> 16;
}

BlittableProp PackProp(Prop input) {
	BlittableProp packed;
	packed.packed_position_and_scale = PackPositionAndScale(input.position, input.scale);
	packed.packed_rotation_dispatch_index_prop_variant_padding = PackRotationAndVariantAndId(input.rotation, 0, 0);
	return packed;
}