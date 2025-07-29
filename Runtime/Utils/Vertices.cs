using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain {
    public struct Vertices {
        public struct Single {
            public float3 position;
            public float3 normal;
            public float4 layers;
            public float4 colour;

            public void AddLerped(float3 startVertex, float3 endVertex, int startIndex, int endIndex, float value, ref VoxelData voxels, ref NativeArray<float3> voxelNormals) {
                position += math.lerp(startVertex, endVertex, value);

                float3 startNormal = voxelNormals[startIndex];
                float3 endNormal = voxelNormals[endIndex];
                normal += math.lerp(startNormal, endNormal, value);

                float4 startLayers = BitUtils.Byte4ToFloat4(voxels.layers[startIndex]);
                float4 endLayers = BitUtils.Byte4ToFloat4(voxels.layers[endIndex]);
                layers += math.lerp(startLayers, endLayers, value);
            }


            public void Add(float3 startVertex, float3 endVertex, int startIndex, int endIndex, ref VoxelData voxels, ref NativeArray<float3> voxelNormals) {
                half start = voxels.densities[startIndex];
                half end = voxels.densities[endIndex];

                float value = math.unlerp(start, end, 0);
                AddLerped(startVertex, endVertex, startIndex, endIndex, value, ref voxels, ref voxelNormals);
            }

            public void Finalize(int count) {
                normal = math.normalizesafe(normal, math.up());
                layers = math.normalizesafe(layers, 0f);
                position /= count;
            }
        }

        public Vertices(int count, Allocator allocator) {
            positions = new NativeArray<float3>(count, allocator, NativeArrayOptions.UninitializedMemory);
            normals = new NativeArray<float3>(count, allocator, NativeArrayOptions.UninitializedMemory);
            layers = new NativeArray<float4>(count, allocator, NativeArrayOptions.UninitializedMemory);
            colours = new NativeArray<float4>(count, allocator, NativeArrayOptions.UninitializedMemory);
        }

        [NativeDisableParallelForRestriction]
        public NativeArray<float3> positions;
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> normals;
        [NativeDisableParallelForRestriction]
        public NativeArray<float4> layers;
        [NativeDisableParallelForRestriction]
        public NativeArray<float4> colours;

        private static void Copy<T>(NativeArray<T> src, NativeArray<T> dst, int dstOffset, int length) where T: unmanaged {
            NativeArray<T> tmpSrc = src.GetSubArray(0, length);
            tmpSrc.CopyTo(dst.GetSubArray(dstOffset, length));
        }

        public void CopyTo(Vertices dst, int dstOffset, int length) {
            Copy(this.positions, dst.positions, dstOffset, length);
            Copy(this.normals, dst.normals, dstOffset, length);
            Copy(this.layers, dst.layers, dstOffset, length);
            Copy(this.colours, dst.colours, dstOffset, length);
        }

        public Vertices GetSubArray(int offset, int length) {
            Vertices tmp = new Vertices();
            tmp.positions = positions.GetSubArray(offset, length);
            tmp.normals = normals.GetSubArray(offset, length);
            tmp.layers = layers.GetSubArray(offset, length);
            tmp.colours = colours.GetSubArray(offset, length);
            return tmp;
        }

        public void SetMeshDataAttributes(int count, Mesh.MeshData data) {
            NativeArray<VertexAttributeDescriptor> descriptors = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp);
            descriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
            descriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1);
            descriptors[2] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 2);

            data.SetVertexBufferParams(count, descriptors);
            positions.GetSubArray(0, count).CopyTo(data.GetVertexData<float3>(0));
            normals.GetSubArray(0, count).CopyTo(data.GetVertexData<float3>(1));
            colours.GetSubArray(0, count).CopyTo(data.GetVertexData<float4>(2));
        }

        public Single this[int index] {
            get {
                return new Single {
                    position = positions[index],
                    normal = normals[index],
                    layers = layers[index],
                    colour = colours[index],
                };
            }
            set {
                positions[index] = value.position;
                normals[index] = value.normal;
                layers[index] = value.layers;
                colours[index] = value.colour;
            }
        }

        public void Dispose() {
            positions.Dispose();
            normals.Dispose();
            layers.Dispose();
            colours.Dispose();
        }
    }
}