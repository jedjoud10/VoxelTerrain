using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static jedjoud.VoxelTerrain.VoxelUtils;

namespace jedjoud.VoxelTerrain.Meshing {
    internal struct ApplyMeshHandler : ISubHandler {
        public NativeReference<MinMaxAABB> bounds;
        public JobHandle jobHandle;
        public Mesh.MeshDataArray array;

        public void Init() {
            bounds = new NativeReference<MinMaxAABB>(Allocator.Persistent);
        }

        public void Schedule(ref MergeMeshHandler merger, ref LightingHandler lighting) {
            bounds.Value = new MinMaxAABB {
                Min = 10000f,
                Max = -10000f,
            };

            BoundsJob boundsJob = new BoundsJob {
                mergedVerticesPositions = merger.mergedVertices.positions,
                totalVertexCount = merger.totalVertexCount,
                bounds = bounds,
            };

            array = Mesh.AllocateWritableMeshData(1);

            SetMeshDataJob setMeshDataJob = new SetMeshDataJob {
                data = array[0],
                
                mergedVertices = merger.mergedVertices,
                mergedIndices = merger.mergedIndices,

                submeshIndexCounts = merger.submeshIndexCounts,
                submeshIndexOffsets = merger.submeshIndexOffsets,
                totalIndexCount = merger.totalIndexCount,
                totalVertexCount = merger.totalVertexCount,
            };

            JobHandle priorHandle = JobHandle.CombineDependencies(merger.jobHandle, lighting.jobHandle);
            JobHandle boundsJobHandle = boundsJob.Schedule(priorHandle);
            JobHandle setMeshDataJobHandle = setMeshDataJob.Schedule(priorHandle);
            jobHandle = JobHandle.CombineDependencies(boundsJobHandle, setMeshDataJobHandle);
        }

        public void Dispose() {
            bounds.Dispose();
        }
    }
}