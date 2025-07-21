using jedjoud.VoxelTerrain.Meshing;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public struct TerrainChunkMesh : IComponentData, IEnableableComponent {
        public NativeArray<float3> vertices;
        public NativeArray<float3> normals;
        public NativeArray<int> mainMeshIndices;
        public JobHandle accessJobHandle;


        public static TerrainChunkMesh FromMeshJobHandlerStats(MeshJobHandler.Stats stats) {
            NativeArray<float3> vertices = new NativeArray<float3>(stats.vertexCount, Allocator.Persistent);
            NativeArray<float3> normals = new NativeArray<float3>(stats.vertexCount, Allocator.Persistent);
            NativeArray<int> indices = new NativeArray<int>(stats.mainMeshIndexCount, Allocator.Persistent);

            /*
            vertices.CopyFrom(stats.vertices.positions);
            normals.CopyFrom(stats.vertices.normals);
            */

            indices.CopyFrom(stats.mainMeshIndices);

            return new TerrainChunkMesh() {
                vertices = vertices,
                normals = normals,
                mainMeshIndices = indices,
            };
        }

        public void Dispose() {
            accessJobHandle.Complete();
            vertices.Dispose();
            normals.Dispose();
            mainMeshIndices.Dispose();
        }
    }
}