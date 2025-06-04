#ifndef PROPS_INSTANCED_INCLUDED
#define PROPS_INSTANCED_INCLUDED

#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Props.cginc"

StructuredBuffer<uint4> _PermBuffer;
StructuredBuffer<uint> _ImpostorIndirectionBuffer;
int _PropType;
int _PermBufferOffset;
int _MaxVariantCountForType;

float3 _CameraPosition;
float3 _CameraUp;

float _ImpostorScale;
float3 _ImpostorOffset;

void CalculateImpostorVectors(float3 billboardPosition, float3 cameraPosition, float3 cameraUp, out float3 forward, out float3 right, out float3 up) {
	// Forward vector: from billboard to camera
	forward = normalize(cameraPosition - billboardPosition);
	forward.y = 0;
	cameraUp = float3(0,1,0);

	// Right vector
	right = normalize(cross(cameraUp, forward));

	// Recompute up vector to ensure orthonormality
	up = cross(forward, right);
}

// VIBe!!!!
float4x4 BillboardLookAtMatrix(float3 billboardPosition, float scale, float3 cameraPosition, float3 cameraUp)
{
	float3 forward, right, up;
	CalculateImpostorVectors(billboardPosition, cameraPosition, cameraUp, forward, right, up);

	// Apply local roll: rotate right and up around forward
	float rollRadians = 3.14 / 2;
	float cosRoll = cos(rollRadians);
	float sinRoll = sin(rollRadians);

	float3 rolledRight = cosRoll * right + sinRoll * up;
	float3 rolledUp = cosRoll * up - sinRoll * right;

	rolledRight *= scale;
	rolledUp *= scale;
	forward *= scale;

	float4x4 what = float4x4(
	    rolledRight.x, rolledUp.x, forward.x, billboardPosition.x,
	    rolledRight.y, rolledUp.y,    forward.y,    billboardPosition.y,
	    rolledRight.z, rolledUp.z,     forward.z,     billboardPosition.z,
	    0, 0, 0, 1.0
	);

	return what;
}

// came in clutch bro gg
// https://gist.github.com/Cyanilux/4046e7bf3725b8f64761bf6cf54a16eb
#if PROCEDURAL_INSTANCING_ON



	// Updates the unity_ObjectToWorld / unity_WorldToObject matrices so our matrix is taken into account

	// Based on : 
	// https://github.com/Unity-Technologies/Graphics/blob/master/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ParticlesInstancing.hlsl
	// and/or
	// https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/master/CGIncludes/UnityStandardParticleInstancing.cginc

	void vertInstancingMatrices(out float4x4 objectToWorld) {
		int propIndex = _ImpostorIndirectionBuffer[unity_InstanceID + _PermBufferOffset];
		uint4 prop = _PermBuffer[propIndex];
		float4 position_scale = UnpackPositionAndScale(prop.xy);
		float4x4 data = BillboardLookAtMatrix(position_scale.xyz + _ImpostorOffset, position_scale.w * _ImpostorScale, _CameraPosition, _CameraUp);
		objectToWorld = data;
	}

	void vertInstancingSetup() {
		vertInstancingMatrices(unity_ObjectToWorld);
	}

#endif

// Just passes the position through, allows us to actually attach this file to the graph.
// Should be placed somewhere in the vertex stage, e.g. right before connecting the object space position.
void Instancing_float(float3 Position, out float3 Out){
	Out = Position;
}

void PropVariantFetch_float(int instance, out float Variant){
	int propIndex = _ImpostorIndirectionBuffer[instance + _PermBufferOffset];
	uint4 prop = _PermBuffer[propIndex];
	uint variant = prop.w & 0xFF;

	// check...
	if (variant >= _MaxVariantCountForType) {
		variant = 0;
	} 

	Variant = (float)variant;
}

float CalculateAngle2D(float2 forward, float2 right)
{
    // Normalize the vectors
    forward = normalize(forward);
    right = normalize(right);

    // The global "up" vector in 2D
    float2 up = float2(0.0, 1.0);

    // Dot product with the global up vector
    float dotted = dot(forward, up);

    // Determinant (signed perpendicular, 2D cross product equivalent)
    float det = dot(right, up);

    // atan2(det, dot) gives the signed angle
    float angle = atan2(det, dotted);

    return angle; // in radians
}

void ImpostorAngleFetch_float(int instance, out float AngleFactor) {
	const float TAU = PI*2;
	int propIndex = _ImpostorIndirectionBuffer[instance + _PermBufferOffset];
	uint4 prop = _PermBuffer[propIndex];
	float4 position_scale = UnpackPositionAndScale(prop.xy);

	float3 forward, right, up;
	CalculateImpostorVectors(position_scale.xyz + _ImpostorOffset, _CameraPosition, _CameraUp, forward, right, up);

	float2 forwardFlat = forward.xz;
	float2 rightFlat = right.xz;
	AngleFactor = (fmod(CalculateAngle2D(forwardFlat, rightFlat) + TAU + PI * 0.5, TAU) / TAU);
}

#endif