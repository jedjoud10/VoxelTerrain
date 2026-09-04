uint2 PackPositionAndScale(float3 position, float scale) {
	uint x = f32tof16(position.x);
	uint y = f32tof16(position.y);
	uint z = f32tof16(position.z);
	uint w = f32tof16(scale);

	uint first = x | (y << 16);
	uint second = z | (w << 16);
	return uint2(first, second);
}

float4 UnpackPositionAndScale(uint2 packed) {
	float x = f16tof32(packed.x & 0xFFFF);
	float y = f16tof32(packed.x >> 16);
	float z = f16tof32(packed.y & 0xFFFF);
	float w = f16tof32(packed.y >> 16);
	return float4(x, y, z, w);
}

uint2 PackRotationAndVariantAndId(float4 rotation, int variant, int id) {
	rotation = normalize(rotation);
	rotation += 1;
	rotation *= 0.5;
	rotation *= 255;
	uint4 packedRotation = (uint4)rotation;

	variant = variant & 0xFF;
	id = clamp(id, 0, 0xFFFFFF);

	uint first = packedRotation.x | (packedRotation.y << 8) | (packedRotation.z << 16) | (packedRotation.w << 24);
	uint second = variant | (uint(id) << 8);

	return uint2(first, second);
}

float4 UnpackRotation(uint2 packed) {
	uint first = packed.x;

	uint4 packedRot;
	packedRot.x = first & 0xFF;
	packedRot.y = (first >> 8) & 0xFF;
	packedRot.z = (first >> 16) & 0xFF;
	packedRot.w = (first >> 24) & 0xFF;

	return normalize((((float4)packedRot) / 255.0) * 2 - 1);
}

float4x4 QuatToMatrix(float4 q) {
    float4x4 rotMat = float4x4
    (
        float4(1 - 2 * q.y * q.y - 2 * q.z * q.z, 2 * q.x * q.y + 2 * q.w * q.z, 2 * q.x * q.z - 2 * q.w * q.y, 0),
        float4(2 * q.x * q.y - 2 * q.w * q.z, 1 - 2 * q.x * q.x - 2 * q.z * q.z, 2 * q.y * q.z + 2 * q.w * q.x, 0),
        float4(2 * q.x * q.z + 2 * q.w * q.y, 2 * q.y * q.z - 2 * q.w * q.x, 1 - 2 * q.x * q.x - 2 * q.y * q.y, 0),
        float4(0, 0, 0, 1)
    );
    return rotMat;
}

float4x4 MakeTRSMatrix(float3 pos, float4 rotQuat, float3 scale) {
    float4x4 rotPart = QuatToMatrix(rotQuat);
    float4x4 trPart = float4x4(float4(scale.x, 0, 0, 0), float4(0, scale.y, 0, 0), float4(0, 0, scale.z, 0), float4(pos, 1));
    return mul(rotPart, trPart);
}

float4x4 UnpackPropToMatrix(uint4 prop) {
	float4 position_scale;
	float4 rotation;

	position_scale = UnpackPositionAndScale(prop.xy);
	rotation = UnpackRotation(prop.zw);

    return transpose(MakeTRSMatrix(position_scale.xyz, rotation, position_scale.w));
}