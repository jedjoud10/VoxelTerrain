using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Meshing {
    public class MeshJobHandler {
        public struct Stats {
            public bool empty;
            public Bounds bounds;
            public int[] forcedSkirtFacesTriCount;
            public int vertexCount;
            public int mainMeshIndexCount;
            public Vertices vertices;
            public NativeArray<int> mainMeshIndices;
        }

        // Sub handlers scheduled in sequential order
        private McCodeHandler code;
        private NormalsHandler normals;
        private CoreSnHandler core;
        private SkirtSnHandler skirt;
        private MergeMeshHandler merger;
        private LightingHandler lighting;
        private ApplyMeshHandler apply;

        private VoxelData voxels;
        private JobHandle jobHandle;
        private Entity entity;

        public MeshJobHandler() {
            voxels = new VoxelData(Allocator.Persistent);
            code.Init();
            normals.Init();
            core.Init();
            skirt.Init();
            merger.Init();
            apply.Init();
        }

        public bool Free { get; private set; } = true;

        public bool IsComplete(EntityManager manager) {
            return jobHandle.IsCompleted && !Free && manager.Exists(entity);
        }

        public void BeginJob(Entity entity, ref TerrainChunkVoxels chunkVoxels, EntityManager mgr) {
            Free = false;
            this.entity = entity;

            JobHandle dependency = voxels.CopyFromAsync(chunkVoxels.data, chunkVoxels.asyncWriteJobHandle);
            chunkVoxels.asyncReadJobHandle = dependency;
            chunkVoxels.meshingInProgress = true;

            code.Schedule(ref voxels, dependency);
            normals.Schedule(ref voxels, dependency);
            core.Schedule(ref voxels, ref normals, ref code);
            skirt.Schedule(ref voxels, ref normals, ref core, dependency);
            merger.Schedule(ref core, ref skirt);
            lighting.Schedule(ref voxels, ref merger, dependency, entity, mgr);
            apply.Schedule(ref merger, ref lighting);
            jobHandle = apply.jobHandle;
        }

        public bool TryComplete(EntityManager mgr, out Mesh outChunkMesh, out Entity entity, out Stats stats) {
            jobHandle.Complete();
            Free = true;

            if (!mgr.Exists(this.entity)) {
                entity = Entity.Null;
                stats = default;
                outChunkMesh = null;
                return false;
            }

            entity = this.entity;

            int[] temp = skirt.skirtForcedTriangleCounter.ToArray();
            bool empty = core.triangleCounter.Count == 0 && temp.All(x => x == 0);

            if (empty) {
                outChunkMesh = null;
                apply.array.Dispose();
            } else {
                outChunkMesh = new Mesh();
                Mesh.ApplyAndDisposeWritableMeshData(apply.array, outChunkMesh, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
            }

            TerrainChunkVoxels tmp = mgr.GetComponentData<TerrainChunkVoxels>(entity);
            tmp.meshingInProgress = false;
            mgr.SetComponentData(entity, tmp);

            stats = new Stats {
                bounds = new Bounds() {
                    min = apply.bounds.Value.Min,
                    max = apply.bounds.Value.Max,
                },

                vertexCount = merger.totalVertexCount.Value,
                mainMeshIndexCount = merger.submeshIndexCounts[0],
                forcedSkirtFacesTriCount = temp,
                empty = empty,

                vertices = merger.mergedVertices.GetSubArray(0, merger.totalVertexCount.Value),
                mainMeshIndices = merger.mergedIndices.GetSubArray(0, merger.submeshIndexCounts[0]),
            };                       

            return true;
        }

        public void Dispose() {
            jobHandle.Complete();
            voxels.Dispose();
            code.Dispose();
            normals.Dispose();
            core.Dispose();
            skirt.Dispose();
            apply.Dispose();
        }
    }
}