using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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

        // Native buffers for mesh data
        public NativeArray<float3> vertices;
        public NativeArray<float3> normals;
        public NativeArray<float2> uvs;
        public NativeArray<int> tempTriangles;
        public NativeArray<int> permTriangles;
        public UnsafePtrList<Voxel> neighbourPtrs;

        // Native buffer for mesh generation data
        public NativeArray<int> indices;
        public NativeArray<byte> enabled;
        public Unsafe.NativeMultiCounter countersQuad;
        public Unsafe.NativeCounter counter;
        public Unsafe.NativeMultiCounter voxelCounters;

        // Native buffer for handling multiple materials
        public NativeParallelHashMap<byte, int> materialHashMap;
        public NativeParallelHashSet<byte> materialHashSet;
        public NativeArray<int> materialSegmentOffsets;
        public Unsafe.NativeCounter materialCounter;
        public JobHandle finalJobHandle;
        public VoxelChunk chunk;
        public PendingMeshJob request;
        public long startingTick;
        public NativeArray<uint> buckets;

        internal NativeArray<VertexAttributeDescriptor> vertexAttributeDescriptors;

        public const int INNER_LOOP_BATCH_COUNT = 128;

        internal MeshJobHandler() {
            // Native buffers for mesh data
            int materialCount = VoxelUtils.MAX_MATERIAL_COUNT;
            voxels = new NativeArray<Voxel>(VoxelUtils.VOLUME, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            vertices = new NativeArray<float3>(VoxelUtils.VOLUME_OFFSET, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            normals = new NativeArray<float3>(VoxelUtils.VOLUME_OFFSET, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            uvs = new NativeArray<float2>(VoxelUtils.VOLUME_OFFSET, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            tempTriangles = new NativeArray<int>(VoxelUtils.VOLUME_OFFSET * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            permTriangles = new NativeArray<int>(VoxelUtils.VOLUME_OFFSET * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            voxelCounters = new Unsafe.NativeMultiCounter(materialCount, Allocator.Persistent);

            // Native buffer for mesh generation data
            indices = new NativeArray<int>(VoxelUtils.VOLUME_OFFSET, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            enabled = new NativeArray<byte>(VoxelUtils.VOLUME_OFFSET, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            countersQuad = new Unsafe.NativeMultiCounter(materialCount, Allocator.Persistent);
            counter = new Unsafe.NativeCounter(Allocator.Persistent);

            // Native buffer for handling multiple materials
            materialHashMap = new NativeParallelHashMap<byte, int>(materialCount, Allocator.Persistent);
            materialHashSet = new NativeParallelHashSet<byte>(materialCount, Allocator.Persistent);
            materialSegmentOffsets = new NativeArray<int>(materialCount, Allocator.Persistent);
            materialCounter = new Unsafe.NativeCounter(Allocator.Persistent);

            VertexAttributeDescriptor positionDesc = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
            VertexAttributeDescriptor normalDesc = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1);
            VertexAttributeDescriptor uvDesc = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 2);
            vertexAttributeDescriptors = new NativeArray<VertexAttributeDescriptor>(new VertexAttributeDescriptor[] { positionDesc, normalDesc, uvDesc }, Allocator.Persistent);

            neighbourPtrs = new UnsafePtrList<Voxel>(7, Allocator.Persistent);
            buckets = new NativeArray<uint>(8, Allocator.Persistent);
        }
        public bool Free { get; private set; } = true;

        // Begin the vertex + quad job that will generate the mesh
        internal JobHandle BeginJob(JobHandle dependency, NativeArray<Voxel>[] neighbours, bool3 neighbourMask) {
            countersQuad.Reset();
            counter.Count = 0;
            materialCounter.Count = 0;
            materialHashSet.Clear();
            materialHashMap.Clear();
            Free = false;

            unsafe {
                neighbourPtrs.Clear();
                foreach (NativeArray<Voxel> v in neighbours) {
                    if (v.IsCreated) {
                        neighbourPtrs.Add(v.GetUnsafeReadOnlyPtr<Voxel>());
                    } else {
                        neighbourPtrs.Add(System.IntPtr.Zero);
                    }
                }
            }

            // Handles fetching MC corners for the SN edges
            CornerJob cornerJob = new CornerJob {
                voxels = voxels,
                enabled = enabled,
                neighbours = neighbourPtrs,
                neighbourMask = neighbourMask,
            };

            // Welcome back material job!
            MaterialJob materialJob = new MaterialJob {
                voxels = voxels,
                neighbours = neighbourPtrs,
                buckets = buckets,
                neighbourMask = neighbourMask,
            };

            // Hello little material indexer
            MaterialIndexerJob materialIndexerJob = new MaterialIndexerJob {
                buckets = buckets,
                materialCounter = materialCounter,
                materialHashMap = materialHashMap,
            };

            // Generate the vertices of the mesh
            // Executed only onces, and shared by multiple submeshes
            VertexJob vertexJob = new VertexJob {
                enabled = enabled,
                voxels = voxels,
                indices = indices,
                vertices = vertices,
                normals = normals,
                uvs = uvs,
                counter = counter,
                neighbours = neighbourPtrs,
                voxelScale = VoxelUtils.VoxelSizeFactor,
                vertexScale = 1.0f,
                size = VoxelUtils.SIZE,
                neighbourMask = neighbourMask,
            };

            // Generate the quads of the mesh (handles materials internally)
            QuadJob quadJob = new QuadJob {
                enabled = enabled,
                voxels = voxels,
                vertexIndices = indices,
                counters = countersQuad,
                neighbours = neighbourPtrs,
                triangles = tempTriangles,
                materialHashMap = materialHashMap.AsReadOnly(),
                materialCounter = materialCounter,
                neighbourMask = neighbourMask,
            };

            // Create sum job to calculate offsets for each material type 
            SumJob sumJob = new SumJob {
                materialCounter = materialCounter,
                materialSegmentOffsets = materialSegmentOffsets,
                countersQuad = countersQuad
            };

            // Create a copy job that will copy temp memory to perm memory
            CopyJob copyJob = new CopyJob {
                materialSegmentOffsets = materialSegmentOffsets,
                tempTriangles = tempTriangles,
                permTriangles = permTriangles,
                materialCounter = materialCounter,
                counters = countersQuad,
            };

            // Material job and indexer job
            JobHandle materialJobHandle = materialJob.Schedule(VoxelUtils.VOLUME_OFFSET, 2048 * 8 * INNER_LOOP_BATCH_COUNT, dependency);
            JobHandle materialIndexerJobHandle = materialIndexerJob.Schedule(materialJobHandle);

            // Start the corner job and material job
            JobHandle cornerJobHandle = cornerJob.Schedule(VoxelUtils.VOLUME_OFFSET, 2048 * INNER_LOOP_BATCH_COUNT, dependency);

            // Start the vertex job
            JobHandle vertexDep = JobHandle.CombineDependencies(cornerJobHandle, dependency);
            JobHandle vertexJobHandle = vertexJob.Schedule(VoxelUtils.VOLUME_OFFSET, 2048 * INNER_LOOP_BATCH_COUNT, vertexDep);

            // Start the quad job
            JobHandle merged = JobHandle.CombineDependencies(vertexJobHandle, cornerJobHandle, materialIndexerJobHandle);
            JobHandle quadJobHandle = quadJob.Schedule(VoxelUtils.VOLUME_OFFSET, 2048 * INNER_LOOP_BATCH_COUNT, merged);

            // Start the sum job 
            JobHandle sumJobHandle = sumJob.Schedule(quadJobHandle);

            // Start the copy job
            JobHandle copyJobHandle = copyJob.Schedule(VoxelUtils.MAX_MATERIAL_COUNT, 32, sumJobHandle);

            finalJobHandle = copyJobHandle;
            return finalJobHandle;
        }

        // Complete the jobs and return a mesh
        internal VoxelMesh Complete(Mesh mesh) {
            if (voxels == null || chunk == null) {
                return default;
            }

            finalJobHandle.Complete();
            Free = true;

            // Get the max number of materials we generated for this mesh
            int maxMaterials = materialCounter.Count;

            // Get the max number of vertices (shared by submeshes)
            int maxVertices = counter.Count;

            // Count the max number of indices (sum of all submesh indices)
            int maxIndices = 0;

            // Count the number of indices we will have in maximum (all material indices combined)
            for (int i = 0; i < maxMaterials; i++) {
                maxIndices += countersQuad[i] * 6;
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
                counters = countersQuad,
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
                int countIndices = countersQuad[i] * 6;
                int segmentOffset = materialSegmentOffsets[i];

                if (countIndices > 0) {
                    lookup2[i] = (lookup[i], segmentOffset);
                } else {
                    // null...
                    lookup2[i] = (byte.MaxValue, segmentOffset);
                }
            }

            chunk = null;
            return new VoxelMesh {
                VoxelMaterialsLookup = lookup,
                TriangleOffsetLocalMaterials = lookup2,
                ComputeCollisions = request.collisions,
                VertexCount = maxVertices,
                TriangleCount = maxIndices / 3,
            };
        }

        // Dispose of the underlying memory allocations
        internal void Dispose() {
            voxels.Dispose();
            indices.Dispose();
            vertices.Dispose();
            normals.Dispose();
            uvs.Dispose();
            counter.Dispose();
            countersQuad.Dispose();
            tempTriangles.Dispose();
            permTriangles.Dispose();
            materialCounter.Dispose();
            materialHashMap.Dispose();
            materialHashSet.Dispose();
            materialSegmentOffsets.Dispose();
            vertexAttributeDescriptors.Dispose();
            enabled.Dispose();
            voxelCounters.Dispose();
            neighbourPtrs.Dispose();
            buckets.Dispose();
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
        public Unsafe.NativeMultiCounter counters;
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