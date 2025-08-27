using Unity.Collections;
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

                float4 startLayers = BitUtils.UnpackUnorm8(voxels.layers[startIndex]);
                float4 endLayers = BitUtils.UnpackUnorm8(voxels.layers[endIndex]);
                layers += math.lerp(startLayers, endLayers, value);
                //layers += math.select(startLayers, endLayers, value > 0.5f);
            }


            public void Add(float3 startVertex, float3 endVertex, int startIndex, int endIndex, ref VoxelData voxels, ref NativeArray<float3> voxelNormals) {
                half start = voxels.densities[startIndex];
                half end = voxels.densities[endIndex];

                float value = math.unlerp(start, end, 0);
                AddLerped(startVertex, endVertex, startIndex, endIndex, value, ref voxels, ref voxelNormals);
            }

            public void Finalize(int count) {
                normal = math.normalizesafe(normal, math.up());
                layers /= count;
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
            NativeArray<VertexAttributeDescriptor> descriptors = new NativeArray<VertexAttributeDescriptor>(4, Allocator.Temp);
            descriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float16, 4, 0);
            descriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.SNorm8, 4, 1);
            descriptors[2] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 2);
            descriptors[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.UNorm8, 4, 3);
            data.SetVertexBufferParams(count, descriptors);


            var dstPositions = data.GetVertexData<half4>(0);
            for (int i = 0; i < count; i++) {
                dstPositions[i] = (half4)new float4(positions[i], 0);
            }

            var dstNormals = data.GetVertexData<uint>(1);
            for (int i = 0; i < count; i++) {
                dstNormals[i] = BitUtils.PackSnorm8(new float4(normals[i], 0));
            }

            var dstColours = data.GetVertexData<uint>(2);
            for (int i = 0; i < count; i++) {
                dstColours[i] = BitUtils.PackUnorm8(colours[i]);
            }

            var dstLayers = data.GetVertexData<uint>(3);
            for (int i = 0; i < count; i++) {
                dstLayers[i] = BitUtils.PackUnorm8(layers[i]);
            }
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