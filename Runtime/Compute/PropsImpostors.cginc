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

// came in clutch bro gg
// https://gist.github.com/Cyanilux/4046e7bf3725b8f64761bf6cf54a16eb
#if PROCEDURAL_INSTANCING_ON
	// VIBe!!!!
	float4x4 BillboardLookAtMatrix(float3 billboardPosition, float scale, float3 cameraPosition, float3 cameraUp)
	{
	    // Forward vector: from billboard to camera
	    float3 forward = normalize(cameraPosition - billboardPosition);
		cameraUp = float3(0, 1, 0);
		forward.y = 0;
	
	    // Right vector: perpendicular to forward and up
	    float3 right = normalize(cross(cameraUp, forward));
	
	    // Recompute up to ensure orthogonality
	    float3 up = cross(forward, right);

		right *= scale;
		up *= scale;
		forward *= scale;

	
	    // Compose the 4x4 matrix (rotation + translation)
	    return float4x4(
	        right.x, up.x, forward.x, billboardPosition.x,
	        right.y, up.y, forward.y, billboardPosition.y,
	        right.z, up.z, forward.z, billboardPosition.z,
	        0,         0,           0,           1
	    );
	}

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

#endif