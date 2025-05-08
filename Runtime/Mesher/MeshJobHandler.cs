using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using jedjoud.VoxelTerrain.Unsafe;

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
        public NativeMultiCounter countersQuad;
        public NativeCounter counter;
        public NativeMultiCounter voxelCounters;

        // Native buffer for handling multiple materials
        public NativeParallelHashMap<byte, int> materialHashMap;
        public NativeParallelHashSet<byte> materialHashSet;
        public NativeArray<int> materialSegmentOffsets;
        public NativeCounter materialCounter;
        public JobHandle finalJobHandle;
        public VoxelMesher.MeshingRequest request;
        public long startingTick;
        public NativeArray<uint> buckets;
        public NativeArray<float3> bounds;
        public VoxelMesher mesher;
        public JobHandle vertexJobHandle, quadJobHandle;

        internal NativeArray<VertexAttributeDescriptor> vertexAttributeDescriptors;

        public const int INNER_LOOP_BATCH_COUNT = 64;
        const int VOL = 65 * 65 * 65;
        private bool blocky;

        internal MeshJobHandler(VoxelMesher mesher) {
            vertexJobHandle = default;
            quadJobHandle = default;
            this.mesher = mesher;
            this.blocky = mesher.blocky;


            // Native buffers for mesh data
            int materialCount = 256;
            voxels = new NativeArray<Voxel>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            vertices = new NativeArray<float3>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            normals = new NativeArray<float3>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            uvs = new NativeArray<float2>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            tempTriangles = new NativeArray<int>(VOL * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            permTriangles = new NativeArray<int>(VOL * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            voxelCounters = new NativeMultiCounter(materialCount, Allocator.Persistent);

            // Native buffer for mesh generation data
            indices = new NativeArray<int>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            enabled = new NativeArray<byte>(VOL, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            countersQuad = new NativeMultiCounter(materialCount, Allocator.Persistent);
            counter = new NativeCounter(Allocator.Persistent);

            // Native buffer for handling multiple materials
            materialHashMap = new NativeParallelHashMap<byte, int>(materialCount, Allocator.Persistent);
            materialHashSet = new NativeParallelHashSet<byte>(materialCount, Allocator.Persistent);
            materialSegmentOffsets = new NativeArray<int>(materialCount, Allocator.Persistent);
            materialCounter = new NativeCounter(Allocator.Persistent);

            VertexAttributeDescriptor positionDesc = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
            VertexAttributeDescriptor normalDesc = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1);
            VertexAttributeDescriptor uvDesc = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 2);
            vertexAttributeDescriptors = new NativeArray<VertexAttributeDescriptor>(new VertexAttributeDescriptor[] { positionDesc, normalDesc, uvDesc }, Allocator.Persistent);

            // We can't discard 0,0,0 since we start at -1,-1,-1, which kinda makes remapping hard. Wtv
            neighbourPtrs = new UnsafePtrList<Voxel>(27, Allocator.Persistent);

            buckets = new NativeArray<uint>(8, Allocator.Persistent);
            bounds = new NativeArray<float3>(2, Allocator.Persistent);
        }
        public bool Free { get; private set; } = true;

        // Begin the vertex + quad job that will generate the mesh
        internal JobHandle BeginJob(JobHandle dependency) {

            float voxelSizeFactor = mesher.terrain.voxelSizeFactor;
            countersQuad.Reset();
            counter.Count = 0;
            materialCounter.Count = 0;
            materialHashSet.Clear();
            materialHashMap.Clear();
            bounds[0] = new float3(65 * voxelSizeFactor);
            bounds[1] = new float3(0.0);
            Free = false;

            BitField32 mask = new BitField32(uint.MinValue);
            mask.SetBits(13, true);
            unsafe {
                neighbourPtrs.Clear();
                for (int i = 0; i < 27; i++) {
                    neighbourPtrs.Add(System.IntPtr.Zero);
                }
            }

            // Handles fetching MC corners for the SN edges
            CornerJob cornerJob = new CornerJob {
                voxels = voxels,
                enabled = enabled,
            };

            // Welcome back material job!
            MaterialJob materialJob = new MaterialJob {
                voxels = voxels,
                buckets = buckets,
                neighbours = neighbourPtrs,
                neighbourMask = mask,
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
                indices = indices,
                vertices = vertices,
                normals = normals,
                uvs = uvs,
                counter = counter,
                voxelScale = voxelSizeFactor,
                blocky = blocky,
            };

            // Calculate vertex ambient occlusion 
            AmbientOcclusionJob aoJob = new AmbientOcclusionJob {
                counter = counter,
                normals = normals,
                uvs = uvs,
                vertices = vertices,
                voxels = voxels,
                globalOffset = mesher.aoGlobalOffset,
                globalSpread = mesher.aoGlobalSpread,
                minDotNormal = mesher.aoMinDotNormal,
                strength = mesher.aoStrength,
                voxelScale = voxelSizeFactor,
                neighbours = neighbourPtrs,
                neighbourMask = mask,
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
                counters = countersQuad,
                triangles = tempTriangles,
                materialHashMap = materialHashMap.AsReadOnly(),
                materialCounter = materialCounter,
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
            JobHandle materialJobHandle = materialJob.Schedule(VOL, 2048 * 8 * INNER_LOOP_BATCH_COUNT, dependency);
            JobHandle materialIndexerJobHandle = materialIndexerJob.Schedule(materialJobHandle);
            
            // Start the corner job and material job
            JobHandle cornerJobHandle = cornerJob.Schedule(VOL, 2048 * INNER_LOOP_BATCH_COUNT, dependency);

            // Start the vertex job
            JobHandle vertexDep = JobHandle.CombineDependencies(cornerJobHandle, dependency);
            vertexJobHandle = vertexJob.Schedule(VOL, 2048 * INNER_LOOP_BATCH_COUNT, vertexDep);
            JobHandle boundsJobHandle = boundsJob.Schedule(vertexJobHandle);
            JobHandle aoJobHandle = aoJob.Schedule(VOL, 2048 * INNER_LOOP_BATCH_COUNT, vertexJobHandle);

            // Start the quad job
            JobHandle merged = JobHandle.CombineDependencies(vertexJobHandle, cornerJobHandle, materialIndexerJobHandle);
            quadJobHandle = quadJob.Schedule(VOL, 2048 * INNER_LOOP_BATCH_COUNT, merged);

            // Start the sum job 
            JobHandle sumJobHandle = sumJob.Schedule(quadJobHandle);

            // Start the copy job
            JobHandle copyJobHandle = copyJob.Schedule(256, 32, sumJobHandle);
            finalJobHandle = JobHandle.CombineDependencies(copyJobHandle, boundsJobHandle, aoJobHandle);
            return finalJobHandle;
        }

        // Complete the jobs and return a mesh
        internal VoxelMesh Complete(Mesh mesh) {
            if (voxels == null || request.chunk == null) {
                return default;
            }

            finalJobHandle.Complete();
            vertexJobHandle = default;
            quadJobHandle = default;
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

            return new VoxelMesh {
                VoxelMaterialsLookup = lookup,
                TriangleOffsetLocalMaterials = lookup2,
                ComputeCollisions = request.collisions,
                VertexCount = maxVertices,
                TriangleCount = maxIndices / 3,
                Bounds = new Bounds() {
                    min = bounds[0],
                    max = bounds[1],
                }
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
            bounds.Dispose();
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