using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;
using UnityEngine.Rendering;
using static jedjoud.VoxelTerrain.VoxelUtils;

namespace jedjoud.VoxelTerrain.Meshing {
    internal struct ApplyMeshHandler : ISubHandler {
        public NativeArray<float3> mergedVertices;
        public NativeArray<float3> mergedNormals;
        public NativeArray<int> mergedIndices;
        
        public NativeReference<MinMaxAABB> bounds;
        public NativeArray<VertexAttributeDescriptor> vertexAttributeDescriptors;
        
        public JobHandle jobHandle;
        
        public NativeArray<int> submeshIndexOffsets;
        public NativeArray<int> submeshIndexCounts;
        public NativeReference<int> totalVertexCount;
        public NativeReference<int> totalIndexCount;
        
        public Mesh.MeshDataArray array;
        public bool arrayAllocated;

        public void Init() {
            mergedVertices = new NativeArray<float3>(VOLUME, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            mergedNormals = new NativeArray<float3>(VOLUME, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            mergedIndices = new NativeArray<int>(VOLUME * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            VertexAttributeDescriptor positionDesc = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
            VertexAttributeDescriptor normalDesc = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1);
            vertexAttributeDescriptors = new NativeArray<VertexAttributeDescriptor>(new VertexAttributeDescriptor[] { positionDesc, normalDesc }, Allocator.Persistent);
            bounds = new NativeReference<MinMaxAABB>(Allocator.Persistent);

            submeshIndexOffsets = new NativeArray<int>(7, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            submeshIndexCounts = new NativeArray<int>(7, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            totalVertexCount = new NativeReference<int>(Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            totalIndexCount = new NativeReference<int>(Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        public void Schedule(ref CoreSnHandler core, ref SkirtSnHandler skirt) {
            totalVertexCount.Value = 0;
            totalIndexCount.Value = 0;

            bounds.Value = new MinMaxAABB {
                Min = 10000f,
                Max = -10000f,
            };

            MergeMeshJob mergeMeshJob = new MergeMeshJob {
                vertices = core.vertices,
                normals = core.normals,
                indices = core.indices,

                vertexCounter = core.vertexCounter,
                triangleCounter = core.triangleCounter,

                skirtVertices = skirt.skirtVertices,
                skirtNormals = skirt.skirtNormals,

                skirtStitchedIndices = skirt.skirtStitchedIndices,
                skirtForcedPerFaceIndices = skirt.skirtForcedPerFaceIndices,

                skirtVertexCounter = skirt.skirtVertexCounter,

                skirtStitchedTriangleCounter = skirt.skirtStitchedTriangleCounter,
                skirtForcedTriangleCounter = skirt.skirtForcedTriangleCounter,

                submeshIndexCounts = submeshIndexCounts,
                submeshIndexOffsets = submeshIndexOffsets,
                totalIndexCount = totalIndexCount,
                totalVertexCount = totalVertexCount,

                mergedVertices = mergedVertices,
                mergedNormals = mergedNormals,
                mergedIndices = mergedIndices,
            };

            BoundsJob boundsJob = new BoundsJob {
                mergedVertices = mergedVertices,
                totalVertexCount = totalVertexCount,
                bounds = bounds,
            };

            array = Mesh.AllocateWritableMeshData(1);
            arrayAllocated = true;

            SetMeshDataJob setMeshDataJob = new SetMeshDataJob {
                data = array[0],
                vertexAttributeDescriptors = vertexAttributeDescriptors,

                mergedVertices = mergedVertices,
                mergedNormals = mergedNormals,
                mergedIndices = mergedIndices,

                submeshIndexCounts = submeshIndexCounts,
                submeshIndexOffsets = submeshIndexOffsets,
                totalIndexCount = totalIndexCount,
                totalVertexCount = totalVertexCount,
            };

            JobHandle dependencies = JobHandle.CombineDependencies(skirt.skirtQuadJobHandle, core.quadJobHandle);
            JobHandle mergeMeshJobHandle = mergeMeshJob.Schedule(dependencies);
            JobHandle boundsJobHandle = boundsJob.Schedule(mergeMeshJobHandle);
            JobHandle setMeshDataJobHandle = setMeshDataJob.Schedule(mergeMeshJobHandle);
            jobHandle = JobHandle.CombineDependencies(boundsJobHandle, setMeshDataJobHandle);
        }

        public void Dispose() {
            vertexAttributeDescriptors.Dispose();
            bounds.Dispose();

            submeshIndexCounts.Dispose();
            submeshIndexOffsets.Dispose();
            totalIndexCount.Dispose();
            totalVertexCount.Dispose();

            mergedVertices.Dispose();
            mergedNormals.Dispose();
            mergedIndices.Dispose();

            if (arrayAllocated)
                array.Dispose();
        }
    }
}