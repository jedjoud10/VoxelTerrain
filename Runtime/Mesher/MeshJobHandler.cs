using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Meshing {
    internal class MeshJobHandler {
        public struct Stats {
            public bool empty;
            public Bounds bounds;
            public int[] forcedSkirtFacesTriCount;
            public int vertexCount;
            public int indexCount;
            public NativeArray<float3> vertices;
            public NativeArray<int> indices;
        }

        // Sub handlers scheduled in sequential order
        private McCodeHandler code;
        private NormalsHandler normals;
        private CoreSnHandler core;
        private SkirtSnHandler skirt;
        private ApplyMeshHandler apply;

        private NativeArray<Voxel> voxels;
        private JobHandle jobHandle;
        private Entity entity;

        public MeshJobHandler() {
            voxels = new NativeArray<Voxel>(VoxelUtils.VOLUME, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            code.Init();
            normals.Init();
            core.Init();
            skirt.Init();
            apply.Init();
        }

        public bool Free { get; private set; } = true;

        public bool IsComplete(EntityManager manager) {
            return jobHandle.IsCompleted && !Free && manager.Exists(entity);
        }

        public void BeginJob(Entity entity, NativeArray<Voxel> srcVoxels, JobHandle dependency) {
            Free = false;
            this.entity = entity;

            dependency = new AsyncMemCpy<Voxel> { src = srcVoxels, dst = this.voxels }.Schedule(dependency);
            code.Schedule(voxels, dependency);
            normals.Schedule(voxels, dependency);
            core.Schedule(voxels, ref normals, ref code);
            skirt.Schedule(voxels, ref normals, ref core, dependency);
            apply.Schedule(ref core, ref skirt);
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
            bool empty = core.vertexCounter.Count == 0 && core.triangleCounter.Count == 0 && temp.All(x => x == 0) && skirt.skirtVertexCounter.Count == 0;

            if (empty) {
                outChunkMesh = null;
                apply.array.Dispose();
            } else {
                outChunkMesh = new Mesh();
                Mesh.ApplyAndDisposeWritableMeshData(apply.array, outChunkMesh, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
            }

            stats = new Stats {
                bounds = new Bounds() {
                    min = apply.bounds.Value.Min,
                    max = apply.bounds.Value.Max,
                },

                vertexCount = apply.totalVertexCount.Value,
                indexCount = apply.submeshIndexCounts[0],
                forcedSkirtFacesTriCount = temp,
                empty = empty,

                vertices = apply.mergedVertices.GetSubArray(0, apply.totalVertexCount.Value),
                indices = apply.mergedIndices.GetSubArray(0, apply.submeshIndexCounts[0]),
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