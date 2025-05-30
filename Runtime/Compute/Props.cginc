uint2 PackPositionAndScale(float3 position, float scale) {
	uint x = f32tof16(position.x);
	uint y = f32tof16(position.y);
	uint z = f32tof16(position.z);
	uint w = f32tof16(scale);

	uint first = x | (y << 16);
	uint second = z | (w << 16);
	return uint2(first, second);
}

uint2 PackRotationAndVariant(float4 rotation, int variant) {
	rotation = normalize(rotation);
	rotation += 1;
	rotation *= 0.5;
	rotation *= 255;
	uint4 packedRotation = (uint4)rotation;

	variant = variant & 0xFF;

	uint first = packedRotation.x | (packedRotation.y << 8) | (packedRotation.z << 16) | (packedRotation.w << 24);
	uint second = variant;

	// TODO: USE THE REMAINING 3 BYTES!!!
	return uint2(first, second);
}