#ifndef INSTANCED_FETCH_INCLUDED
#define INSTANCED_FETCH_INCLUDED

StructuredBuffer<float3> _Vertices;
StructuredBuffer<float3> _Normals;
StructuredBuffer<float3> _Colors;
StructuredBuffer<int> _Indices;

void MyFunctionA_float(float i, out float3 position, out float3 normal, out float3 color)
{
    int index = _Indices[int(i)];
    position = _Vertices[index];
    normal = _Normals[index];
    color = _Colors[index];
}

#endif