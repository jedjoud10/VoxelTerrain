using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
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
        public NativeArray<int> tempTriangles;
        public NativeArray<int> permTriangles;
        public NativeArray<int> indices;
        public NativeArray<byte> enabled;
        public NativeMultiCounter quadCounters;
        public NativeCounter counter;
        public NativeMultiCounter voxelCounters;

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

        // Native buffer for handling multiple materials
        public NativeParallelHashMap<byte, int> materialHashMap;
        public NativeParallelHashSet<byte> materialHashSet;
        public NativeArray<int> materialSegmentOffsets;
        public NativeCounter materialCounter;

        // Other misc stuff
        public JobHandle finalJobHandle;
        private Entity entity;
        public NativeArray<uint> buckets;
        public NativeArray<float3> bounds;

        internal NativeArray<VertexAttributeDescriptor> vertexAttributeDescriptors;


        public NativeList<float3> debugData;

        const int BATCH_SIZE = VoxelUtils.VOLUME * 2;
        const int VOL = VoxelUtils.VOLUME;
        const int MATS = VoxelUtils.MAX_MATERIAL_COUNT;

        internal MeshJobHandler() {
            
            // Native buffers for copied voxel data and generated normal data
            voxels = new NativeArray<Voxel>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            voxelNormals = new NativeArray<float3>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // Native buffers for mesh data
            vertices = new NativeArray<float3>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            normals = new NativeArray<float3>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            uvs = new NativeArray<float2>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            tempTriangles = new NativeArray<int>(VOL * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            permTriangles = new NativeArray<int>(VOL * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            voxelCounters = new NativeMultiCounter(MATS, Allocator.Persistent);
            indices = new NativeArray<int>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            enabled = new NativeArray<byte>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            quadCounters = new NativeMultiCounter(MATS, Allocator.Persistent);
            counter = new NativeCounter(Allocator.Persistent);

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

            // Native buffer for handling multiple materials
            materialHashMap = new NativeParallelHashMap<byte, int>(MATS, Allocator.Persistent);
            materialHashSet = new NativeParallelHashSet<byte>(MATS, Allocator.Persistent);
            materialSegmentOffsets = new NativeArray<int>(MATS, Allocator.Persistent);
            materialCounter = new NativeCounter(Allocator.Persistent);

            VertexAttributeDescriptor positionDesc = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
            VertexAttributeDescriptor normalDesc = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1);
            VertexAttributeDescriptor uvDesc = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 2);
            vertexAttributeDescriptors = new NativeArray<VertexAttributeDescriptor>(new VertexAttributeDescriptor[] { positionDesc, normalDesc, uvDesc }, Allocator.Persistent);

            buckets = new NativeArray<uint>(8, Allocator.Persistent);
            bounds = new NativeArray<float3>(2, Allocator.Persistent);
        }

        public bool Free { get; private set; } = true;

        public bool IsComplete(EntityManager manager) {
            return finalJobHandle.IsCompleted && !Free && manager.Exists(entity);
        }

        // Begin the vertex + quad job that will generate the mesh
        internal JobHandle BeginJob(Entity entity, NativeArray<Voxel> voxels) {
            this.entity = entity;
            JobHandle dependency = new AsyncMemCpy<Voxel> { src = voxels, dst = this.voxels }.Schedule();



            debugData.Clear();
            //float voxelSizeFactor = mesher.terrain.voxelSizeFactor;
            float voxelSizeFactor = 1f;
            quadCounters.Reset();
            skirtTriangleCounter.Count = 0;
            skirtVertexCounter.Count = 0;
            counter.Count = 0;
            materialCounter.Count = 0;
            materialHashSet.Clear();
            materialHashMap.Clear();
            bounds[0] = new float3(VoxelUtils.SIZE * voxelSizeFactor);
            bounds[1] = new float3(0.0);
            Free = false;

            // Normalize my shi dawg
            NormalsJob normalsJob = new NormalsJob {
                normals = voxelNormals,
                voxels = voxels,
            };

            // Handles fetching MC corners for the SN edges
            CornerJob cornerJob = new CornerJob {
                voxels = voxels,
                enabled = enabled,
            };

            // Welcome back material job!
            MaterialJob materialJob = new MaterialJob {
                voxels = voxels,
                buckets = buckets,
            };

            // Hello little material indexer
            MaterialIndexerJob materialIndexerJob = new MaterialIndexerJob {
                buckets = buckets,
                materialCounter = materialCounter,
                materialHashMap = materialHashMap,
            };

            // Generate the vertices of the mesh
            // Executed only once, and shared by multiple submeshes
            VertexJob vertexJob = new VertexJob {
                enabled = enabled,
                voxels = voxels,
                voxelNormals = voxelNormals,
                indices = indices,
                vertices = vertices,
                normals = normals,
                uvs = uvs,
                counter = counter,
                voxelScale = voxelSizeFactor,
            };

            // Calculate the AABB for the chunk using another job
            BoundsJob boundsJob = new BoundsJob {
                vertices = vertices,
                counter = counter,
                bounds = bounds,
            };

            // Generate the quads of the mesh (handles materials internally)
            QuadJob quadJob = new QuadJob {
                enabled = enabled,
                voxels = voxels,
                vertexIndices = indices,
                counters = quadCounters,
                triangles = tempTriangles,
                materialHashMap = materialHashMap.AsReadOnly(),
                materialCounter = materialCounter,
            };

            // Create sum job to calculate offsets for each material type 
            SumJob sumJob = new SumJob {
                materialCounter = materialCounter,
                materialSegmentOffsets = materialSegmentOffsets,
                countersQuad = quadCounters
            };

            // Create a copy job that will copy temp memory to perm memory
            CopyJob copyJob = new CopyJob {
                materialSegmentOffsets = materialSegmentOffsets,
                tempTriangles = tempTriangles,
                permTriangles = permTriangles,
                materialCounter = materialCounter,
                counters = quadCounters,
            };

            // Create a copy job that will copy boundary vertices and indices to the skirts' face values
            SkirtCopyRemapJob skirtCopyJob = new SkirtCopyRemapJob {
                skirtVertexIndicesCopied = skirtVertexIndicesCopied,
                skirtVertices = skirtVertices,
                sourceVertexIndices = indices,
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
            JobHandle normalsJobHandle = normalsJob.Schedule(VOL, BATCH_SIZE, dependency);

            // Material job and indexer job
            JobHandle materialJobHandle = materialJob.Schedule(VOL, BATCH_SIZE, dependency);
            JobHandle materialIndexerJobHandle = materialIndexerJob.Schedule(materialJobHandle);
            
            // Start the corner job and material job
            JobHandle cornerJobHandle = cornerJob.Schedule(VOL, BATCH_SIZE, dependency);

            // Start the vertex job
            JobHandle vertexDep = JobHandle.CombineDependencies(cornerJobHandle, dependency, normalsJobHandle);
            JobHandle vertexJobHandle = vertexJob.Schedule(VOL, BATCH_SIZE, vertexDep);
            JobHandle boundsJobHandle = boundsJob.Schedule(vertexJobHandle);
            //JobHandle aoJobHandle = aoJob.Schedule(VOL, BATCH_SIZE / 16, vertexJobHandle);

            // Copy boundary skirt vertices and start creating skirts
            JobHandle skirtJobHandle = default;

            // Keep track of the voxels that are near the surface (does a 5x5 box-blur like lookup in 2D to check for surface)
            JobHandle closestSurfaceJobHandle = skirtClosestSurfaceThresholdJob.Schedule(VoxelUtils.FACE * 6, BATCH_SIZE, dependency);

            // Copies vertices from the boundary in the source mesh to our skirt vertices. also sets proper indices in the skirtVertexIndicesCopied array
            JobHandle skirtCopyJobHandle = skirtCopyJob.Schedule(vertexJobHandle);

            // Creates skirt vertices (both normal and forced). needs to run at VoxelUtils.SKIRT_FACE since it has a padding of 2 (for edge case on the boundaries)
            JobHandle skirtVertexJobHandle = skirtVertexJob.Schedule(VoxelUtils.SKIRT_FACE * 6, BATCH_SIZE, JobHandle.CombineDependencies(skirtCopyJobHandle, normalsJobHandle, closestSurfaceJobHandle));
            //JobHandle skirtVertexAoJobHandle = skirtAoJob.Schedule(VoxelUtils.SKIRT_FACE * 6, BATCH_SIZE, JobHandle.CombineDependencies(skirtCopyJobHandle, skirtVertexJobHandle));

            // Creates quad based on the copied vertices and skirt-generated vertices
            JobHandle skirtQuadJobHandle = skirtQuadJob.Schedule(VoxelUtils.FACE * 6, BATCH_SIZE, skirtVertexJobHandle);
            //skirtJobHandle = JobHandle.CombineDependencies(skirtQuadJobHandle);
            skirtJobHandle = skirtQuadJobHandle;            

            JobHandle merged = JobHandle.CombineDependencies(vertexJobHandle, cornerJobHandle, materialIndexerJobHandle);
            JobHandle quadJobHandle = quadJob.Schedule(VOL, BATCH_SIZE, merged);

            JobHandle sumJobHandle = sumJob.Schedule(quadJobHandle);
            JobHandle copyJobHandle = copyJob.Schedule(VoxelUtils.MAX_MATERIAL_COUNT, 32, sumJobHandle);

            JobHandle mainDependencies = JobHandle.CombineDependencies(copyJobHandle, boundsJobHandle);
            finalJobHandle = JobHandle.CombineDependencies(mainDependencies, skirtJobHandle);

            return finalJobHandle;
        }

        // Complete the jobs and return a mesh
        internal bool TryComplete(EntityManager mgr, out Mesh mesh, out Entity entity, out VoxelMesh stats) {
            finalJobHandle.Complete();
            Free = true;

            if (counter.Count == 0 || quadCounters[0] == 0 || !mgr.Exists(this.entity)) {
                entity = Entity.Null;
                stats = default;
                mesh = null;
                return false;
            }

            //skirt.Complete(skirtVertices, skirtNormals, skirtUvs, skirtIndices, skirtVertexCounter.Count, skirtTriangleCounter.Count);
            mesh = new Mesh();
            entity = this.entity;

            // Get the max number of materials we generated for this mesh
            int maxMaterials = materialCounter.Count;

            // Get the max number of vertices (shared by submeshes)
            int maxVertices = counter.Count;

            // Count the max number of indices (sum of all submesh indices)
            int maxIndices = 0;

            // Count the number of indices we will have in maximum (all material indices combined)
            for (int i = 0; i < maxMaterials; i++) {
                maxIndices += quadCounters[i] * 6;
            }

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
                permTriangles = permTriangles.Slice(0, maxIndices),
                maxMaterials = maxMaterials,
                maxVertices = maxVertices,
                maxIndices = maxIndices,
                counters = quadCounters,
                materialSegmentOffsets = materialSegmentOffsets,
                data = data,
            };

            // TODO: asyncify this
            test.Schedule().Complete();

            Mesh.ApplyAndDisposeWritableMeshData(array, mesh, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);

            // Create a material array for the new materials
            // This will allow us to map submesh index -> material index
            byte[] lookup = new byte[maxMaterials];

            // Convert material index to material *count* index
            foreach (var item in materialHashMap) {
                lookup[item.Value] = item.Key;
            }

            // This lookup table will allow us to find the index of a material given the triangle it hit using the triangle offset range
            // (since the submeshes triangles are all sequential)
            (byte, int)[] lookup2 = new (byte, int)[maxMaterials];

            // Set mesh submeshes
            for (int i = 0; i < maxMaterials; i++) {
                int countIndices = quadCounters[i] * 6;
                int segmentOffset = materialSegmentOffsets[i];

                if (countIndices > 0) {
                    lookup2[i] = (lookup[i], segmentOffset);
                } else {
                    // null...
                    lookup2[i] = (byte.MaxValue, segmentOffset);
                }
            }

            stats = new VoxelMesh {
                VoxelMaterialsLookup = lookup,
                TriangleOffsetLocalMaterials = lookup2,
                VertexCount = maxVertices,
                TriangleCount = maxIndices / 3,
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
            indices.Dispose();
            vertices.Dispose();
            normals.Dispose();
            uvs.Dispose();
            counter.Dispose();
            quadCounters.Dispose();
            tempTriangles.Dispose();
            permTriangles.Dispose();
            materialCounter.Dispose();
            materialHashMap.Dispose();
            materialHashSet.Dispose();
            materialSegmentOffsets.Dispose();
            vertexAttributeDescriptors.Dispose();
            enabled.Dispose();
            voxelCounters.Dispose();
            buckets.Dispose();
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
        public int maxMaterials;
        [ReadOnly]
        public NativeArray<VertexAttributeDescriptor> vertexAttributeDescriptors;
        [ReadOnly]
        public NativeSlice<int> permTriangles;
        [ReadOnly]
        public NativeMultiCounter counters;
        [ReadOnly]
        public NativeArray<int> materialSegmentOffsets;

        public void Execute() {
            data.SetVertexBufferParams(maxVertices, vertexAttributeDescriptors);

            vertices.CopyTo(data.GetVertexData<float3>(0)); 
            normals.CopyTo(data.GetVertexData<float3>(1)); 
            uvs.CopyTo(data.GetVertexData<float2>(2)); 

            // Set mesh indices
            data.SetIndexBufferParams(maxIndices, IndexFormat.UInt32);
            permTriangles.CopyTo(data.GetIndexData<int>());
            data.subMeshCount = maxMaterials;


            for (int i = 0; i < maxMaterials; i++) {
                int countIndices = counters[i] * 6;
                int segmentOffset = materialSegmentOffsets[i];

                if (countIndices > 0) {
                    data.SetSubMesh(i, new SubMeshDescriptor {
                        indexStart = segmentOffset,
                        indexCount = countIndices,
                        topology = MeshTopology.Triangles,
                    }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                }
            }
        }
    }
}