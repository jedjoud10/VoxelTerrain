using System;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Meshing {
    // Contains the allocation data for a single job
    // There are multiple instances of this class stored inside the voxel mesher to saturate the other threads
    internal class MeshJobHandler {
        public struct Stats {
            public bool empty;
            public Bounds bounds;
            public int[] forcedSkirtFacesTriCount;
            public int vertexCount;
            public int indexCount;
        }

        // Copy of the voxel data that we will use for meshing
        private NativeArray<Voxel> voxels;

        // Normals that are calculated based on the inputted voxels
        // Split into two different jobs to improve cache locality (hopefully)
        // Array of 4 elements; base,x,y,z
        private NativeArray<half>[] normalPrefetchedVals;
        private NativeArray<float3> voxelNormals;

        // Native buffers for mesh data
        private NativeArray<float3> vertices;

        private NativeArray<float3> normals;
        private NativeArray<int> indices;
        private NativeArray<int> vertexIndices;
        private NativeArray<byte> enabled;
        private NativeArray<uint> bits;
        private NativeCounter triangleCounter;
        private NativeCounter vertexCounter;

        private NativeArray<float3> skirtVertices;
        private NativeArray<float3> skirtNormals;
        private NativeCounter skirtVertexCounter;

        private NativeArray<bool> skirtWithinThreshold;
        private NativeArray<int> skirtVertexIndicesCopied;
        private NativeArray<int> skirtVertexIndicesGenerated;
        private NativeArray<int> skirtStitchedIndices;
        private NativeArray<int> skirtForcedPerFaceIndices;


        private NativeCounter skirtStitchedTriangleCounter;
        private NativeMultiCounter skirtForcedTriangleCounter;

        // Other misc stuff
        private JobHandle finalJobHandle;
        private Entity entity;
        private NativeArray<float3> bounds;

        private NativeArray<VertexAttributeDescriptor> vertexAttributeDescriptors;
        private NativeList<float3> debugData;

        const int VOL = VoxelUtils.VOLUME;
        private Mesh.MeshDataArray array;

        internal MeshJobHandler() {
            // Native buffers for copied voxel data and generated normal data
            voxels = new NativeArray<Voxel>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            voxelNormals = new NativeArray<float3>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            normalPrefetchedVals = new NativeArray<half>[4];
            for (int i = 0; i < 4; i++) {
                normalPrefetchedVals[i] = new NativeArray<half>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            int packedCount = (int)math.ceil((float)VOL / (8 * sizeof(uint)));
            bits = new NativeArray<uint>(packedCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // Native buffers for mesh data
            vertices = new NativeArray<float3>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            normals = new NativeArray<float3>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            indices = new NativeArray<int>(VOL * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            vertexIndices = new NativeArray<int>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            enabled = new NativeArray<byte>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            triangleCounter = new NativeCounter(Allocator.Persistent);
            vertexCounter = new NativeCounter(Allocator.Persistent);

            debugData = new NativeList<float3>(1000, Allocator.Persistent);

            /*
            // Skirt vertices that were generated using 2D surface nets or 1D surface nets
            // These are the vertices that were NOT forced (generated normally)
            skirtStitchedVertices = new NativeArray<float3>(VoxelUtils.SKIRT_FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtStitchedNormals = new NativeArray<float3>(VoxelUtils.SKIRT_FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtStitchedVertexCounter = new NativeCounter(Allocator.Persistent);

            // Skirt vertices that were forcefully generated using the distance metric
            skirtForcedVertices = new NativeArray<float3>(VoxelUtils.SKIRT_FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtForcedNormals = new NativeArray<float3>(VoxelUtils.SKIRT_FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtForcedVertexCounter = new NativeCounter(Allocator.Persistent);
            */

            skirtVertices = new NativeArray<float3>(VoxelUtils.SKIRT_FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtNormals = new NativeArray<float3>(VoxelUtils.SKIRT_FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtVertexCounter = new NativeCounter(Allocator.Persistent);

            // Dedicated vertex index lookup buffers for the copied vertices from the boundary and generated skirted vertices
            skirtVertexIndicesCopied = new NativeArray<int>(VoxelUtils.FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtVertexIndicesGenerated = new NativeArray<int>(VoxelUtils.SKIRT_FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // Stored sequentially, used for the main skirt mesh (submesh = 0)
            // Uses the skirtStitchedIndexCounter as next ptr
            // Since there can be 2 quads in each perpendicular direction, we must multiply by 2 desu
            // Uses indices that refer to vertices stored in the main mesh NOT THE COPIED VERTICES!!!!
            skirtStitchedIndices = new NativeArray<int>(VoxelUtils.SKIRT_FACE * 2 * 6 * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // Stored with gaps, since it will be copied to the submeshes' triangle array at an offset
            // Each face can reserve up to VoxelUtils.SKIRT_FACE * 6 indices for itself, so since we have 6 faces, we mult by 6
            skirtForcedPerFaceIndices = new NativeArray<int>(VoxelUtils.SKIRT_FACE * 6 * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            
            skirtStitchedTriangleCounter = new NativeCounter(Allocator.Persistent);
            skirtForcedTriangleCounter = new NativeMultiCounter(6, Allocator.Persistent);

            skirtWithinThreshold = new NativeArray<bool>(VoxelUtils.FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            VertexAttributeDescriptor positionDesc = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
            VertexAttributeDescriptor normalDesc = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1);
            VertexAttributeDescriptor uvDesc = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 2);
            vertexAttributeDescriptors = new NativeArray<VertexAttributeDescriptor>(new VertexAttributeDescriptor[] { positionDesc, normalDesc, uvDesc }, Allocator.Persistent);

            bounds = new NativeArray<float3>(2, Allocator.Persistent);
        }

        public bool Free { get; private set; } = true;

        public bool IsComplete(EntityManager manager) {
            return finalJobHandle.IsCompleted && !Free && manager.Exists(entity);
        }

        // Begin the vertex + quad job that will generate the mesh
        internal void BeginJob(Entity entity, NativeArray<Voxel> srcVoxels, JobHandle dependency) {
            dependency = new AsyncMemCpy<Voxel> { src = srcVoxels, dst = this.voxels }.Schedule(dependency);

            Free = false;
            this.entity = entity;

            debugData.Clear();
            //float voxelSizeFactor = mesher.terrain.voxelSizeFactor;
            float voxelSizeFactor = 1f;
            triangleCounter.Count = 0;
            vertexCounter.Count = 0;


            skirtStitchedTriangleCounter.Count = 0;
            skirtForcedTriangleCounter.Reset();
            skirtVertexCounter.Count = 0;

            //skirtVertexCounter.Count = 0;
            bounds[0] = new float3(VoxelUtils.SIZE * voxelSizeFactor);
            bounds[1] = new float3(0.0);

            // Normalize my shi dawg | Part 1
            NormalsPrefetchJob prefetchBase = new NormalsPrefetchJob {
                voxels = voxels.GetSubArray(0, VOL),
                val = normalPrefetchedVals[0],
            };
            NormalsPrefetchJob prefetchX = new NormalsPrefetchJob {
                voxels = voxels.GetSubArray(1, VOL - 1),
                val = normalPrefetchedVals[1],
            };
            NormalsPrefetchJob prefetchY = new NormalsPrefetchJob {
                voxels = voxels.GetSubArray(VoxelUtils.SIZE * VoxelUtils.SIZE, VOL - VoxelUtils.SIZE * VoxelUtils.SIZE),
                val = normalPrefetchedVals[2],
            };
            NormalsPrefetchJob prefetchZ = new NormalsPrefetchJob {
                voxels = voxels.GetSubArray(VoxelUtils.SIZE, VOL - VoxelUtils.SIZE),
                val = normalPrefetchedVals[3],
            };

            // Normalize my shi dawg | Part 2
            NormalsCalculateJob normalsCalculateJob = new NormalsCalculateJob {
                normals = voxelNormals,
                baseVal = normalPrefetchedVals[0],
                xVal = normalPrefetchedVals[1],
                yVal = normalPrefetchedVals[2],
                zVal = normalPrefetchedVals[3]
            };


            // Handles fetching MC corners for the SN edges
            CornerJob cornerJob = new CornerJob {
                bits = bits,
                enabled = enabled,
            };

            CheckJob checkJob = new CheckJob {
                voxels = voxels,
                bits = bits,
            };

            // Generate the vertices of the mesh
            // Executed only once, and shared by multiple submeshes
            VertexJob vertexJob = new VertexJob {
                enabled = enabled,
                voxels = voxels,
                voxelNormals = voxelNormals,
                indices = vertexIndices,
                vertices = vertices,
                normals = normals,
                vertexCounter = vertexCounter,
                voxelScale = voxelSizeFactor,
            };

            // Calculate the AABB for the chunk using another job
            BoundsJob boundsJob = new BoundsJob {
                vertices = vertices,
                vertexCounter = vertexCounter,
                bounds = bounds,
            };

            // Generate the quads of the mesh (handles materials internally)
            QuadJob quadJob = new QuadJob {
                enabled = enabled,
                voxels = voxels,
                vertexIndices = vertexIndices,
                triangleCounter = triangleCounter,
                triangles = indices,
            };

            // Job that acts like an SDF generator, checks if certain positions are within a certain distance from a surface (for forced skirt generation)
            SkirtClosestSurfaceJob skirtClosestSurfaceThresholdJob = new SkirtClosestSurfaceJob {
                voxels = voxels,
                withinThreshold = skirtWithinThreshold,
            };

            // Create a copy job that will copy the boundary indices of the original mesh (needed for skirt quad job)
            SkirtCopyVertexIndicesJob skirtCopyVertexIndicesJob = new SkirtCopyVertexIndicesJob {
                skirtVertexIndicesCopied = skirtVertexIndicesCopied,
                sourceVertexIndices = vertexIndices,
            };

            // Create the skirt vertices in one of the chunk's face
            SkirtVertexJob skirtVertexJob = new SkirtVertexJob {
                skirtVertexIndicesGenerated = skirtVertexIndicesGenerated,
                skirtVertices = skirtVertices,
                withinThreshold = skirtWithinThreshold,
                skirtVertexCounter = skirtVertexCounter,
                voxels = voxels,
                voxelNormals = voxelNormals,
                skirtNormals = skirtNormals,
                voxelScale = voxelSizeFactor,
                vertexCounter = vertexCounter,
            };

            // Create skirt quads
            SkirtQuadJob skirtQuadJob = new SkirtQuadJob {
                skirtVertexIndicesCopied = skirtVertexIndicesCopied,
                skirtVertexIndicesGenerated = skirtVertexIndicesGenerated,
                skirtForcedPerFaceIndices = skirtForcedPerFaceIndices,
                skirtForcedTriangleCounter = skirtForcedTriangleCounter,
                skirtStitchedTriangleCounter = skirtStitchedTriangleCounter,
                skirtStitchedIndices = skirtStitchedIndices,
                voxels = voxels,
                debugData = debugData.AsParallelWriter(),
            };

            const int BATCH = VoxelUtils.VOLUME;
            const int SMALLER_BATCH = VoxelUtils.VOLUME / 2;
            const int SMALLEST_BATCH = VoxelUtils.VOLUME / 4;
            const int SKIRT_BATCH = VoxelUtils.SKIRT_FACE * 6;
            const int SMALLER_SKIRT_BATCH = VoxelUtils.SKIRT_FACE / 2;
            const int PER_SKIRT_FACE_BATCH = VoxelUtils.SKIRT_FACE / 6;
            const int PER_SKIRT_FACE_SMALLER_BATCH = VoxelUtils.SKIRT_FACE / 12;

            // Voxel finite-diffed normals job
            JobHandle prefetchBaseJobHandle = prefetchBase.Schedule(VOL, SMALLER_BATCH, dependency);
            JobHandle prefetchXJobHandle = prefetchX.Schedule(VOL - 1, SMALLER_BATCH, dependency);
            JobHandle prefetchYJobHandle = prefetchY.Schedule(VOL - VoxelUtils.SIZE * VoxelUtils.SIZE, SMALLER_BATCH, dependency);
            JobHandle prefetchZJobHandle = prefetchZ.Schedule(VOL - VoxelUtils.SIZE, SMALLER_BATCH, dependency);
            JobHandle normalsDep1 = JobHandle.CombineDependencies(prefetchXJobHandle, prefetchYJobHandle, prefetchZJobHandle);
            JobHandle normalsDep2 = JobHandle.CombineDependencies(normalsDep1, prefetchBaseJobHandle);
            JobHandle normalsJobHandle = normalsCalculateJob.Schedule(VOL, SMALLEST_BATCH, normalsDep2);

            // Start the corner job and material job
            JobHandle checkJobHandle = checkJob.Schedule(bits.Length, SMALLEST_BATCH, dependency);
            JobHandle cornerJobHandle = cornerJob.Schedule(VOL, SMALLEST_BATCH, checkJobHandle);

            // Start the vertex job
            JobHandle vertexDep = JobHandle.CombineDependencies(cornerJobHandle, normalsJobHandle);
            JobHandle vertexJobHandle = vertexJob.Schedule(VOL, SMALLEST_BATCH, vertexDep);

            // Start the main mesh quad job
            JobHandle quadJobHandle = quadJob.Schedule(VOL, SMALLEST_BATCH, vertexJobHandle);

            // Keep track of the voxels that are near the surface (does a 5x5 box-blur like lookup in 2D to check for surface)
            JobHandle closestSurfaceJobHandle = skirtClosestSurfaceThresholdJob.Schedule(VoxelUtils.FACE * 6, SMALLER_SKIRT_BATCH, dependency);

            // Copies vertex indices from the boundary in the source mesh to our skirt vertices. We only need to copy indices since we are using submeshes for our skirts, so they all share the same vertex buffer
            JobHandle skirtCopyJobHandle = skirtCopyVertexIndicesJob.Schedule(vertexJobHandle);

            // Creates skirt vertices (both normal and forced). needs to run at VoxelUtils.SKIRT_FACE since it has a padding of 2 (for edge case on the boundaries)
            JobHandle skirtVertexJobHandle = skirtVertexJob.Schedule(VoxelUtils.SKIRT_FACE * 6, PER_SKIRT_FACE_SMALLER_BATCH, JobHandle.CombineDependencies(skirtCopyJobHandle, closestSurfaceJobHandle));
            
            // Creates quad based on the copied vertices and skirt-generated vertices
            JobHandle skirtQuadJobHandle = skirtQuadJob.Schedule(VoxelUtils.FACE * 6, PER_SKIRT_FACE_SMALLER_BATCH, skirtVertexJobHandle);

            // Not linked to the main pipeline but still requires verts access
            JobHandle boundsJobHandle = boundsJob.Schedule(vertexJobHandle);

            // Single job that will create our mesh, that contains multiple submeshes (for each skirt)
            array = Mesh.AllocateWritableMeshData(1);
            SetMeshDataJob setMeshDataJob = new SetMeshDataJob {
                data = array[0],
                vertexAttributeDescriptors = vertexAttributeDescriptors,

                vertices = vertices,
                normals = normals,
                indices = indices,

                vertexCounter = vertexCounter,
                triangleCounter = triangleCounter,

                skirtVertices = skirtVertices,
                skirtNormals = skirtNormals,

                skirtStitchedIndices = skirtStitchedIndices,
                skirtForcedPerFaceIndices = skirtForcedPerFaceIndices,

                skirtVertexCounter = skirtVertexCounter,

                skirtStitchedTriangleCounter = skirtStitchedTriangleCounter,
                skirtForcedTriangleCounter = skirtForcedTriangleCounter,
            };

            JobHandle dependencies = JobHandle.CombineDependencies(skirtQuadJobHandle, quadJobHandle);
            JobHandle setMeshDataJobHandle = setMeshDataJob.Schedule(dependencies);
            finalJobHandle =  JobHandle.CombineDependencies(boundsJobHandle, setMeshDataJobHandle);
        }

        // Complete the jobs and return a mesh
        internal bool TryComplete(EntityManager mgr, out Mesh outChunkMesh, out Entity entity, out Stats stats) {
            finalJobHandle.Complete();
            Free = true;

            if (!mgr.Exists(this.entity) || array.Length == 0) {
                entity = Entity.Null;
                stats = default;
                outChunkMesh = null;
                return false;
            }

            entity = this.entity;

            int[] temp = skirtForcedTriangleCounter.ToArray();
            bool empty = vertexCounter.Count == 0 && triangleCounter.Count == 0 && temp.All(x => x == 0) && skirtVertexCounter.Count == 0;

            if (empty) {
                outChunkMesh = null;
                array.Dispose();
            } else {
                outChunkMesh = new Mesh();
                Mesh.ApplyAndDisposeWritableMeshData(array, outChunkMesh, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
            }

            stats = new Stats {
                bounds = new Bounds() {
                    min = bounds[0],
                    max = bounds[1],
                },

                vertexCount = vertexCounter.Count,
                indexCount = triangleCounter.Count * 3,
                forcedSkirtFacesTriCount = temp,
                empty = empty,
            };                       

            return true;
        }

        // Dispose of the underlying memory allocations
        internal void Dispose() {
            finalJobHandle.Complete();

            /*
            if (array.Length > 0) {
                array.Dispose();
            }
            */

            voxels.Dispose();
            vertexIndices.Dispose();
            vertices.Dispose();
            normals.Dispose();
            vertexCounter.Dispose();
            triangleCounter.Dispose();
            indices.Dispose();
            vertexAttributeDescriptors.Dispose();
            enabled.Dispose();
            bounds.Dispose();
            skirtVertexIndicesCopied.Dispose();
            skirtVertexIndicesGenerated.Dispose();
            skirtForcedTriangleCounter.Dispose();
            skirtStitchedTriangleCounter.Dispose();
            skirtForcedPerFaceIndices.Dispose();
            skirtStitchedIndices.Dispose();
            skirtWithinThreshold.Dispose();
            debugData.Dispose();
            voxelNormals.Dispose();
            bits.Dispose();

            skirtVertices.Dispose();
            skirtNormals.Dispose();
            skirtVertexCounter.Dispose();

            for (int i = 0; i < 4; i++) {
                normalPrefetchedVals[i].Dispose();
            }
        }
    }

}