using System;
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
        // Copy of the voxel data that we will use for meshing
        public NativeArray<Voxel> voxels;

        // Normals that are calculated based on the inputted voxels
        // Normals on the boundary are set to zero, since we can't use finite diffs at the boundary
        public NativeArray<float3> voxelNormals;

        // Native buffers for mesh data
        public NativeArray<float3> vertices;
        public NativeArray<float3> normals;
        public NativeArray<float2> uvs;
        public NativeArray<int> indices;
        public NativeArray<int> vertexIndices;
        public NativeArray<byte> enabled;
        public NativeArray<uint> bits;
        public NativeCounter quadCounter;
        public NativeCounter vertexCounter;

        // Native buffers for skirt mesh data
        // Only for the face that faces the negative x direction for now
        public NativeArray<float3> skirtVertices;
        public NativeArray<float3> skirtNormals;
        public NativeArray<float2> skirtUvs;
        public NativeArray<bool> skirtWithinThreshold;
        public NativeArray<int> skirtVertexIndicesCopied;
        public NativeArray<int> skirtVertexIndicesGenerated;
        public NativeArray<int> skirtIndices;
        public NativeCounter skirtVertexCounter;
        public NativeCounter skirtTriangleCounter;

        // Other misc stuff
        public JobHandle finalJobHandle;
        private Entity entity;
        public NativeArray<float3> bounds;

        internal NativeArray<VertexAttributeDescriptor> vertexAttributeDescriptors;
        public NativeList<float3> debugData;

        public int PER_VOXEL_JOB_BATCH_SIZE = VoxelUtils.VOLUME / 2;
        public int PER_SKIRT_SURFACE_JOB_BATCH_SIZE = VoxelUtils.SKIRT_FACE * 3;

        const int VOL = VoxelUtils.VOLUME;

        internal MeshJobHandler() {
            
            // Native buffers for copied voxel data and generated normal data
            voxels = new NativeArray<Voxel>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            voxelNormals = new NativeArray<float3>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            int packedCount = (int)math.ceil((float)VOL / (8 * sizeof(uint)));
            bits = new NativeArray<uint>(packedCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // Native buffers for mesh data
            vertices = new NativeArray<float3>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            normals = new NativeArray<float3>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            uvs = new NativeArray<float2>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            indices = new NativeArray<int>(VOL * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            vertexIndices = new NativeArray<int>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            enabled = new NativeArray<byte>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            quadCounter = new NativeCounter(Allocator.Persistent);
            vertexCounter = new NativeCounter(Allocator.Persistent);

            debugData = new NativeList<float3>(1000, Allocator.Persistent);

            // Native buffers for skirt mesh data
            skirtVertices = new NativeArray<float3>(VoxelUtils.SKIRT_FACE * 2 * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtNormals = new NativeArray<float3>(VoxelUtils.SKIRT_FACE * 2 * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtUvs = new NativeArray<float2>(VoxelUtils.SKIRT_FACE * 2 * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // Dedicated vertex index lookup buffers for the copied vertices from the boundary and generated skirted vertices
            skirtVertexIndicesCopied = new NativeArray<int>(VoxelUtils.FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtVertexIndicesGenerated = new NativeArray<int>(VoxelUtils.SKIRT_FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            skirtIndices = new NativeArray<int>(VoxelUtils.SKIRT_FACE * 2 * 6 * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtVertexCounter = new NativeCounter(Allocator.Persistent);
            skirtTriangleCounter = new NativeCounter(Allocator.Persistent);
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
        internal JobHandle BeginJob(Entity entity, JobHandle dependency) {
            Free = false;
            this.entity = entity;

            debugData.Clear();
            //float voxelSizeFactor = mesher.terrain.voxelSizeFactor;
            float voxelSizeFactor = 1f;
            quadCounter.Count = 0;
            vertexCounter.Count = 0;
            skirtTriangleCounter.Count = 0;
            skirtVertexCounter.Count = 0;
            bounds[0] = new float3(VoxelUtils.SIZE * voxelSizeFactor);
            bounds[1] = new float3(0.0);


            // Normalize my shi dawg
            /*
            NormalsJob normalsJob = new NormalsJob {
                normals = voxelNormals,
                voxels = voxels,
            };
            */

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
                uvs = uvs,
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
                quadCounter = quadCounter,
                triangles = indices,
            };

            // Create a copy job that will copy boundary vertices and indices to the skirts' face values
            SkirtCopyRemapJob skirtCopyJob = new SkirtCopyRemapJob {
                skirtVertexIndicesCopied = skirtVertexIndicesCopied,
                skirtVertices = skirtVertices,
                sourceVertexIndices = vertexIndices,
                sourceVertices = vertices,
                skirtVertexCounter = skirtVertexCounter,
                sourceNormals = normals,
                skirtNormals = skirtNormals,
                skirtUvs = skirtUvs,
            };

            // Job that acts like an SDF generator, checks if certain positions are within a certain distance from a surface (for forced skirt generation)
            SkirtClosestSurfaceJob skirtClosestSurfaceThresholdJob = new SkirtClosestSurfaceJob {
                voxels = voxels,
                withinThreshold = skirtWithinThreshold,
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
                skirtUvs = skirtUvs,
            };

            // Create skirt quads
            SkirtQuadJob skirtQuadJob = new SkirtQuadJob {
                skirtIndices = skirtIndices,
                skirtVertexIndicesCopied = skirtVertexIndicesCopied,
                skirtVertexIndicesGenerated = skirtVertexIndicesGenerated,
                skirtTriangleCounter = skirtTriangleCounter,
                voxels = voxels,
                debugData = debugData.AsParallelWriter(),
            };

            // Voxel finite-diffed normals job
            //JobHandle normalsJobHandle = normalsJob.Schedule(VOL, PER_VOXEL_JOB_BATCH_SIZE, dependency);

            // Start the corner job and material job
            JobHandle checkJobHandle = checkJob.Schedule(bits.Length, PER_VOXEL_JOB_BATCH_SIZE, dependency);
            JobHandle cornerJobHandle = cornerJob.Schedule(VOL, PER_VOXEL_JOB_BATCH_SIZE, checkJobHandle);

            // Start the vertex job
            JobHandle vertexDep = JobHandle.CombineDependencies(cornerJobHandle, dependency);
            JobHandle vertexJobHandle = vertexJob.Schedule(VOL, PER_VOXEL_JOB_BATCH_SIZE, vertexDep);
            JobHandle boundsJobHandle = boundsJob.Schedule(vertexJobHandle);

            // Copy boundary skirt vertices and start creating skirts
            JobHandle skirtJobHandle = default;

            // Keep track of the voxels that are near the surface (does a 5x5 box-blur like lookup in 2D to check for surface)
            JobHandle closestSurfaceJobHandle = skirtClosestSurfaceThresholdJob.Schedule(VoxelUtils.FACE * 6, PER_SKIRT_SURFACE_JOB_BATCH_SIZE, dependency);

            // Copies vertices from the boundary in the source mesh to our skirt vertices. also sets proper indices in the skirtVertexIndicesCopied array
            JobHandle skirtCopyJobHandle = skirtCopyJob.Schedule(vertexJobHandle);

            // Creates skirt vertices (both normal and forced). needs to run at VoxelUtils.SKIRT_FACE since it has a padding of 2 (for edge case on the boundaries)
            JobHandle skirtVertexJobHandle = skirtVertexJob.Schedule(VoxelUtils.SKIRT_FACE * 6, PER_SKIRT_SURFACE_JOB_BATCH_SIZE, JobHandle.CombineDependencies(skirtCopyJobHandle, closestSurfaceJobHandle));

            // Creates quad based on the copied vertices and skirt-generated vertices
            JobHandle skirtQuadJobHandle = skirtQuadJob.Schedule(VoxelUtils.FACE * 6, PER_SKIRT_SURFACE_JOB_BATCH_SIZE, skirtVertexJobHandle);
            //skirtJobHandle = JobHandle.CombineDependencies(skirtQuadJobHandle);
            skirtJobHandle = skirtQuadJobHandle;            

            JobHandle merged = JobHandle.CombineDependencies(vertexJobHandle, cornerJobHandle, checkJobHandle);
            JobHandle quadJobHandle = quadJob.Schedule(VOL, PER_VOXEL_JOB_BATCH_SIZE, merged);
            JobHandle mainDependencies = JobHandle.CombineDependencies(quadJobHandle, boundsJobHandle);
            finalJobHandle = JobHandle.CombineDependencies(mainDependencies, skirtJobHandle);

            return finalJobHandle;
        }

        // Complete the jobs and return a mesh
        internal bool TryComplete(EntityManager mgr, out Mesh mesh, out Entity entity, out VoxelMesh stats) {
            finalJobHandle.Complete();
            Free = true;

            if (vertexCounter.Count == 0 || quadCounter.Count == 0 || !mgr.Exists(this.entity)) {
                entity = Entity.Null;
                stats = default;
                mesh = null;
                return false;
            }

            //skirt.Complete(skirtVertices, skirtNormals, skirtUvs, skirtIndices, skirtVertexCounter.Count, skirtTriangleCounter.Count);
            mesh = new Mesh();
            entity = this.entity;

            int maxVertices = vertexCounter.Count;
            int maxIndices = quadCounter.Count * 6;

            // Set mesh shared vertices
            mesh.Clear();

            // TODO: batch this
            Mesh.MeshDataArray array = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData data = array[0];

            SetMeshDataJob test = new SetMeshDataJob() {
                vertexAttributeDescriptors = vertexAttributeDescriptors,
                vertices = vertices.Slice(0, maxVertices),
                normals = normals.Slice(0, maxVertices),
                uvs = uvs.Slice(0, maxVertices),
                indices = indices.Slice(0, maxIndices),
                maxVertices = maxVertices,
                maxIndices = maxIndices,
                data = data,
            };

            // TODO: asyncify this
            test.Schedule().Complete();

            Mesh.ApplyAndDisposeWritableMeshData(array, mesh, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);

            stats = new VoxelMesh {
                Bounds = new Bounds() {
                    min = bounds[0],
                    max = bounds[1],
                }
            };
            return true;
        }

        // Dispose of the underlying memory allocations
        internal void Dispose() {
            voxels.Dispose();
            vertexIndices.Dispose();
            vertices.Dispose();
            normals.Dispose();
            uvs.Dispose();
            vertexCounter.Dispose();
            quadCounter.Dispose();
            indices.Dispose();
            vertexAttributeDescriptors.Dispose();
            enabled.Dispose();
            bounds.Dispose();
            skirtVertices.Dispose();
            skirtVertexIndicesCopied.Dispose();
            skirtVertexIndicesGenerated.Dispose();
            skirtVertexCounter.Dispose();
            skirtTriangleCounter.Dispose();
            skirtWithinThreshold.Dispose();
            debugData.Dispose();
            skirtNormals.Dispose();
            voxelNormals.Dispose();
            skirtUvs.Dispose();
            bits.Dispose();
        }
    }

    [BurstCompile]
    public struct SetMeshDataJob : IJob {
        [WriteOnly]
        public Mesh.MeshData data;
        [ReadOnly]
        public NativeSlice<float3> vertices;
        [ReadOnly]
        public NativeSlice<float3> normals;
        [ReadOnly]
        public NativeSlice<float2> uvs;
        public int maxVertices;
        public int maxIndices;
        [ReadOnly]
        public NativeArray<VertexAttributeDescriptor> vertexAttributeDescriptors;
        [ReadOnly]
        public NativeSlice<int> indices;

        public void Execute() {
            data.SetVertexBufferParams(maxVertices, vertexAttributeDescriptors);

            vertices.CopyTo(data.GetVertexData<float3>(0)); 
            normals.CopyTo(data.GetVertexData<float3>(1)); 
            uvs.CopyTo(data.GetVertexData<float2>(2)); 

            data.SetIndexBufferParams(maxIndices, IndexFormat.UInt32);
            indices.CopyTo(data.GetIndexData<int>());

            data.subMeshCount = 1;
            data.SetSubMesh(0, new SubMeshDescriptor {
                indexStart = 0,
                indexCount = maxIndices,
                topology = MeshTopology.Triangles,
            }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
        }
    }
}