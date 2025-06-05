#ifndef PROPS_INSTANCED_INCLUDED
#define PROPS_INSTANCED_INCLUDED

#include "Packages/com.jedjoud.voxelterrain/Runtime/Compute/Props.cginc"

StructuredBuffer<uint4> _PermBuffer;
StructuredBuffer<float4x4> _PermMatricesBuffer;
StructuredBuffer<uint> _InstancedIndirectionBuffer;
int _PropType;
int _PermBufferOffset;
int _MaxVariantCountForType;

// came in clutch bro gg
// https://gist.github.com/Cyanilux/4046e7bf3725b8f64761bf6cf54a16eb
#if PROCEDURAL_INSTANCING_ON

	// Updates the unity_ObjectToWorld / unity_WorldToObject matrices so our matrix is taken into account

	// Based on : 
	// https://github.com/Unity-Technologies/Graphics/blob/master/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ParticlesInstancing.hlsl
	// and/or
	// https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/master/CGIncludes/UnityStandardParticleInstancing.cginc

	void vertInstancingMatrices(out float4x4 objectToWorld, out float4x4 worldToObject) {
		uint propIndex = _InstancedIndirectionBuffer[unity_InstanceID + _PermBufferOffset];
		float4x4 data = _PermMatricesBuffer[propIndex];
		objectToWorld = data;

		// Inverse transform matrix
		float3x3 w2oRotation;
		w2oRotation[0] = objectToWorld[1].yzx * objectToWorld[2].zxy - objectToWorld[1].zxy * objectToWorld[2].yzx;
		w2oRotation[1] = objectToWorld[0].zxy * objectToWorld[2].yzx - objectToWorld[0].yzx * objectToWorld[2].zxy;
		w2oRotation[2] = objectToWorld[0].yzx * objectToWorld[1].zxy - objectToWorld[0].zxy * objectToWorld[1].yzx;

		float det = dot(objectToWorld[0].xyz, w2oRotation[0]);
		w2oRotation = transpose(w2oRotation);
		w2oRotation *= rcp(det);
		float3 w2oPosition = mul(w2oRotation, -objectToWorld._14_24_34);

		worldToObject._11_21_31_41 = float4(w2oRotation._11_21_31, 0.0f);
		worldToObject._12_22_32_42 = float4(w2oRotation._12_22_32, 0.0f);
		worldToObject._13_23_33_43 = float4(w2oRotation._13_23_33, 0.0f);
		worldToObject._14_24_34_44 = float4(w2oPosition, 1.0f);
	}

	void vertInstancingSetup() {
		vertInstancingMatrices(unity_ObjectToWorld, unity_WorldToObject);
	}

#endif

// Just passes the position through, allows us to actually attach this file to the graph.
// Should be placed somewhere in the vertex stage, e.g. right before connecting the object space position.
void Instancing_float(float3 Position, out float3 Out){
	Out = Position;
}

void PropVariantFetch_float(int instance, out float Variant){
	uint propIndex = _InstancedIndirectionBuffer[instance + _PermBufferOffset];
	uint4 prop = _PermBuffer[propIndex];
	uint variant = prop.w & 0xFF;

	// check...
	if (variant >= _MaxVariantCountForType) {
		variant = 0;
	} 

	Variant = (float)variant;
}

#endif