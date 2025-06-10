using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static jedjoud.VoxelTerrain.VoxelUtils;
using static jedjoud.VoxelTerrain.BatchUtils;

namespace jedjoud.VoxelTerrain.Meshing {
    internal struct CoreSnHandler : ISubHandler {
        public NativeArray<float3> vertices;
        public NativeArray<float3> normals;
        public NativeArray<int> indices;
        public NativeArray<int> vertexIndices;
        public NativeCounter vertexCounter;
        public NativeCounter triangleCounter;

        public JobHandle vertexJobHandle;
        public JobHandle quadJobHandle;


        public void Init() {
            vertices = new NativeArray<float3>(VOLUME, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            normals = new NativeArray<float3>(VOLUME, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            indices = new NativeArray<int>(VOLUME * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            vertexIndices = new NativeArray<int>(VOLUME, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            vertexCounter = new NativeCounter(Allocator.Persistent);
            triangleCounter = new NativeCounter(Allocator.Persistent);
        }

        public void Schedule(NativeArray<Voxel> voxels, ref NormalsHandler normalsSubHandler, ref McCodeHandler codeSubHandler) {
            triangleCounter.Count = 0;
            vertexCounter.Count = 0;

            float voxelSizeFactor = 1;

            // Generate the *shared* vertices of the main mesh
            VertexJob vertexJob = new VertexJob {
                enabled = codeSubHandler.enabled,
                voxels = voxels,
                voxelNormals = normalsSubHandler.voxelNormals,
                indices = vertexIndices,
                vertices = vertices,
                normals = normals,
                vertexCounter = vertexCounter,
                voxelScale = voxelSizeFactor,
            };

            // Generate the quads of the mesh (handles materials internally)
            QuadJob quadJob = new QuadJob {
                enabled = codeSubHandler.enabled,
                voxels = voxels,
                vertexIndices = vertexIndices,
                triangleCounter = triangleCounter,
                triangles = indices,
            };

            JobHandle vertexDep = JobHandle.CombineDependencies(normalsSubHandler.jobHandle, codeSubHandler.jobHandle);
            vertexJobHandle = vertexJob.Schedule(VOLUME, EVEN_SMALLER_BATCH, vertexDep);
            quadJobHandle = quadJob.Schedule(VOLUME, EVEN_SMALLER_BATCH, vertexJobHandle);
        }

        public void Dispose() {
            vertices.Dispose();
            normals.Dispose();
            indices.Dispose();
            vertexIndices.Dispose();
            vertexCounter.Dispose();
            triangleCounter.Dispose();
        }
    }
}