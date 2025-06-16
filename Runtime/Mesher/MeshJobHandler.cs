using System;
using System.Linq;
using Unity.Collections;
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
            public int indexCount;
            public NativeArray<float3> vertices;
            public NativeArray<int> indices;
        }

        public struct Request {
            public TerrainChunk chunk;
            public Action<TerrainChunk> callback;
        }

        // Sub handlers scheduled in sequential order
        private McCodeHandler code;
        private NormalsHandler normals;
        private CoreSnHandler core;
        private SkirtSnHandler skirt;
        private ApplyMeshHandler apply;

        private VoxelData voxels;
        private JobHandle jobHandle;
        private Request request;

        public MeshJobHandler() {
            voxels = new VoxelData(Allocator.Persistent);
            code.Init();
            normals.Init();
            core.Init();
            skirt.Init();
            apply.Init();
        }

        public bool Free { get; private set; } = true;

        public bool IsComplete() {
            return jobHandle.IsCompleted && !Free;
        }

        public void BeginJob(Request request) {
            Free = false;
            this.request = request;
            TerrainChunk chunk = request.chunk;

            JobHandle dependency = voxels.CopyFromAsync(chunk.voxels, chunk.asyncReadJobHandle);
            chunk.asyncReadJobHandle = dependency;
            
            code.Schedule(ref voxels, dependency);
            normals.Schedule(ref voxels, dependency);
            core.Schedule(ref voxels, ref normals, ref code);
            skirt.Schedule(ref voxels, ref normals, ref core, dependency);
            apply.Schedule(ref core, ref skirt);
            jobHandle = apply.jobHandle;
        }

        public bool TryComplete(out Request request, out Stats stats) {
            jobHandle.Complete();
            Free = true;

            if (this.request.chunk == null) {
                stats = default;
                request = default;
                return false;
            }

            request = this.request;

            int[] temp = skirt.skirtForcedTriangleCounter.ToArray();
            bool empty = core.triangleCounter.Count == 0 && temp.All(x => x == 0);

            if (empty) {
                this.request.chunk.sharedMesh = null;
                apply.array.Dispose();
            } else {
                this.request.chunk.sharedMesh = new Mesh();
                Mesh.ApplyAndDisposeWritableMeshData(apply.array, this.request.chunk.sharedMesh, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
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