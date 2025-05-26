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
        public NativeArray<Voxel> voxels;

        // Normals that are calculated based on the inputted voxels
        // Split into two different jobs to improve cache locality (hopefully)
        // Array of 4 elements; base,x,y,z
        public NativeArray<half>[] normalPrefetchedVals;
        public NativeArray<float3> voxelNormals;

        // Native buffers for mesh data
        public NativeArray<float3> vertices;

        public NativeArray<float3> normals;
        public NativeArray<int> indices;
        public NativeArray<int> vertexIndices;
        public NativeArray<byte> enabled;
        public NativeArray<uint> bits;
        public NativeCounter triangleCounter;
        public NativeCounter vertexCounter;

        public NativeArray<float3> skirtCopiedVertices;
        public NativeArray<float3> skirtCopiedNormals;

        public NativeArray<float3> skirtStitchedVertices;
        public NativeArray<float3> skirtStitchedNormals;

        public NativeArray<float3> skirtForcedVertices;
        public NativeArray<float3> skirtForcedNormals;

        public NativeArray<bool> skirtWithinThreshold;
        public NativeArray<int> skirtVertexIndicesCopied;
        public NativeArray<int> skirtVertexIndicesGenerated;
        public NativeArray<int> skirtStitchedIndices;
        public NativeArray<int> skirtForcedPerFaceIndices;

        public NativeCounter skirtCopiedVertexCounter;
        public NativeCounter skirtStitchedVertexCount;
        public NativeCounter skirtForcedVertexCounter;

        public NativeCounter skirtStitchedTriangleCounter;
        public NativeMultiCounter skirtForcedTriangleCounter;

        // Other misc stuff
        public JobHandle finalJobHandle;
        private Entity entity;
        public NativeArray<float3> bounds;

        internal NativeArray<VertexAttributeDescriptor> vertexAttributeDescriptors;
        public NativeList<float3> debugData;

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

            // Skirt vertices that were copied from the main mesh (the vertices on the very edges)
            // We need these for the dedicated skirt entities that render the forced triangles. We don't duplicate these on the main mesh when we merge
            skirtCopiedVertices = new NativeArray<float3>(VoxelUtils.SKIRT_FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtCopiedNormals = new NativeArray<float3>(VoxelUtils.SKIRT_FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtCopiedVertexCounter = new NativeCounter(Allocator.Persistent);

            // Skirt vertices that were generated using 2D surface nets or 1D surface nets
            // These are the vertices that were NOT forced (generated normally)
            skirtStitchedVertices = new NativeArray<float3>(VoxelUtils.SKIRT_FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtStitchedNormals = new NativeArray<float3>(VoxelUtils.SKIRT_FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtStitchedVertexCount = new NativeCounter(Allocator.Persistent);

            // Skirt vertices that were forcefully generated using the distance metric
            skirtForcedVertices = new NativeArray<float3>(VoxelUtils.SKIRT_FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtForcedNormals = new NativeArray<float3>(VoxelUtils.SKIRT_FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtForcedVertexCounter = new NativeCounter(Allocator.Persistent);

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
        internal void BeginJob(Entity entity, JobHandle dependency) {
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
                triangleCounter = triangleCounter,
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

            // Copies vertices from the boundary in the source mesh to our skirt vertices. also sets proper indices in the skirtVertexIndicesCopied array
            JobHandle skirtCopyJobHandle = skirtCopyJob.Schedule(vertexJobHandle);

            // Creates skirt vertices (both normal and forced). needs to run at VoxelUtils.SKIRT_FACE since it has a padding of 2 (for edge case on the boundaries)
            JobHandle skirtVertexJobHandle = skirtVertexJob.Schedule(VoxelUtils.SKIRT_FACE * 6, PER_SKIRT_FACE_SMALLER_BATCH, JobHandle.CombineDependencies(skirtCopyJobHandle, closestSurfaceJobHandle));
            
            // Creates quad based on the copied vertices and skirt-generated vertices
            JobHandle skirtQuadJobHandle = skirtQuadJob.Schedule(VoxelUtils.FACE * 6, PER_SKIRT_FACE_SMALLER_BATCH, skirtVertexJobHandle);


            // Not linked to the main pipeline but still requires verts access
            JobHandle boundsJobHandle = boundsJob.Schedule(vertexJobHandle);


            array = Mesh.AllocateWritableMeshData(2);
            SetMeshDataJob setMeshDataJob = new SetMeshDataJob {
                skirtNormals = skirtNormals,

                skirtVertices = skirtVertices,


                skirtStitchedIndices = skirtStitchedIndices,
                skirtStitchedTriangleCounter = skirtStitchedTriangleCounter,
                skirtVertexCounter = skirtVertexCounter,

                vertexAttributeDescriptors = vertexAttributeDescriptors,
                vertices = vertices,
                normals = normals,
                indices = indices,
                vertexCounter = vertexCounter,
                triangleCounter = triangleCounter,
                data = array[0],
                /*
                skirtStitchedTriangleCounter = skirtStitchedTriangleCounter,
                skirtStitchedIndices = skirtStitchedIndices,
                skirtVertices = skirtVertices,
                */
            };

            SetSkirtMeshDataJob setSkirtMeshDataJob = new SetSkirtMeshDataJob {
                vertexAttributeDescriptors = vertexAttributeDescriptors,
                skirtVertices = skirtVertices,
                skirtNormals = skirtNormals,

                skirtForcedPerFaceIndices = skirtForcedPerFaceIndices,
                skirtVertexCounter = skirtVertexCounter,
                skirtForcedTriangleCounter = skirtForcedTriangleCounter,
                data = array[1],
            };

            JobHandle setMeshDataJobHandle = setMeshDataJob.Schedule(JobHandle.CombineDependencies(quadJobHandle, skirtQuadJobHandle));
            JobHandle setSkirtMeshDataJobHandle = setSkirtMeshDataJob.Schedule(skirtQuadJobHandle);
            JobHandle setMeshDataJobs = JobHandle.CombineDependencies(setMeshDataJobHandle, setSkirtMeshDataJobHandle);
            finalJobHandle =  JobHandle.CombineDependencies(boundsJobHandle, setMeshDataJobs);
        }

        // Complete the jobs and return a mesh
        internal bool TryComplete(EntityManager mgr, out Mesh outChunkMesh, out Mesh outSkirtMesh, out Entity entity, out Stats stats) {
            finalJobHandle.Complete();
            Free = true;

            if (!mgr.Exists(this.entity) || array.Length == 0) {
                entity = Entity.Null;
                stats = default;
                outChunkMesh = null;
                outSkirtMesh = null;
                return false;
            }

            entity = this.entity;

            int[] temp = skirtForcedTriangleCounter.ToArray();
            bool empty = vertexCounter.Count == 0 && triangleCounter.Count == 0 && temp.All(x => x == 0) && skirtVertexCounter.Count == 0;

            if (empty) {
                outChunkMesh = null;
                outSkirtMesh = null;
                array.Dispose();
            } else {
                outChunkMesh = new Mesh();
                outSkirtMesh = new Mesh();
                Mesh.ApplyAndDisposeWritableMeshData(array, new Mesh[2] {
                    outChunkMesh, outSkirtMesh
                }, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
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
            skirtVertices.Dispose();
            skirtVertexIndicesCopied.Dispose();
            skirtVertexIndicesGenerated.Dispose();
            skirtVertexCounter.Dispose();
            skirtForcedTriangleCounter.Dispose();
            skirtStitchedTriangleCounter.Dispose();
            skirtForcedPerFaceIndices.Dispose();
            skirtStitchedIndices.Dispose();
            skirtWithinThreshold.Dispose();
            debugData.Dispose();
            skirtNormals.Dispose();
            voxelNormals.Dispose();
            bits.Dispose();
            
            for (int i = 0; i < 4; i++) {
                normalPrefetchedVals[i].Dispose();
            }
        }
    }

    [BurstCompile]
    public struct SetMeshDataJob : IJob {
        [WriteOnly]
        public Mesh.MeshData data;

        [ReadOnly]
        public NativeArray<VertexAttributeDescriptor> vertexAttributeDescriptors;

        [ReadOnly]
        public NativeArray<float3> vertices;
        [ReadOnly]
        public NativeArray<float3> normals;
        [ReadOnly]
        public NativeArray<int> indices;

        [ReadOnly]
        public NativeCounter skirtVertexCounter;
        [ReadOnly]
        public NativeCounter skirtStitchedTriangleCounter;
        [ReadOnly]
        public NativeArray<int> skirtStitchedIndices;
        [ReadOnly]
        public NativeArray<float3> skirtVertices;
        [ReadOnly]
        public NativeArray<float3> skirtNormals;

        [ReadOnly]
        public NativeCounter vertexCounter;
        [ReadOnly]
        public NativeCounter triangleCounter;

        public void Execute() {
            NativeArray<float3> copiedStitchVertices = new NativeArray<float3>(VoxelUtils.SKIRT_FACE * 6, Allocator.Temp);
            NativeArray<float3> copiedStitchNormals = new NativeArray<float3>(VoxelUtils.SKIRT_FACE * 6, Allocator.Temp);
            NativeArray<int> newStitchIndices = new NativeArray<int>(skirtStitchedTriangleCounter.Count * 3, Allocator.Temp);

            // remapped VERTEX indices
            NativeArray<int> stitchVertexIndexRemapper = new NativeArray<int>(skirtVertexCounter.Count, Allocator.Temp);
            NativeBitArray skirtStitchedVerticesBitArray = new NativeBitArray(skirtVertexCounter.Count, Allocator.Temp);

            // We want the merged stitching vertices to be sequentially packed right after the OG mesh's vertices
            // all we're doing is just copying the stitched vertices to the mesh
            int mergedStitchVertexCount = 0;
            int baseOffset = vertexCounter.Count;

            for (int i = 0; i < skirtStitchedTriangleCounter.Count * 3; i++) {
                int index = skirtStitchedIndices[i];

                // check if the index refers to a skirt stich vertex (not a forced vertex and not a copied vertex)
                // change the index to the new strictly stiched index instead
                if (!BitUtils.IsBitSet(index, 31) && !BitUtils.IsBitSet(index, 30)) {
                    index &= ushort.MaxValue;

                    // If the stitch vertex wasn't copied, add it to the "copiedStitch*" arrays
                    if (!skirtStitchedVerticesBitArray.IsSet(index)) {
                        skirtStitchedVerticesBitArray.Set(index, true);

                        float3 vertex = skirtVertices[index];
                        float3 normal = skirtNormals[index];

                        // We will later copy them with an offset into the mesh
                        copiedStitchVertices[mergedStitchVertexCount ] = vertex;
                        copiedStitchNormals[mergedStitchVertexCount] = normal;

                        // We want the skirt vertices to come right after the original mesh vertices
                        stitchVertexIndexRemapper[index] = mergedStitchVertexCount + baseOffset;

                        mergedStitchVertexCount++;
                    }
                } else {
                    stitchVertexIndexRemapper[index] = index;
                }

                // Remap the skirt vertex index
                newStitchIndices[i] = stitchVertexIndexRemapper[index];
            }

            int ogMaxVerticesCnt = vertexCounter.Count;
            int ogMaxIndicesCnt = triangleCounter.Count * 3;

            int skirtVerticesCnt = skirtVertexCounter.Count;
            int skirtIndicesCnt = skirtStitchedTriangleCounter.Count * 3;

            int mergedVerticesCnt = ogMaxIndicesCnt + skirtVerticesCnt;
            int mergedIndicesCnt = ogMaxIndicesCnt + skirtIndicesCnt;

            // copy that vertex into a new vertex buffer dedicated for stitched vertices (NOT the copied ones)





            data.SetVertexBufferParams(ogMaxVerticesCnt, vertexAttributeDescriptors);

            vertices.GetSubArray(0, ogMaxVerticesCnt).CopyTo(data.GetVertexData<float3>(0)); 
            normals.GetSubArray(0, ogMaxVerticesCnt).CopyTo(data.GetVertexData<float3>(1)); 

            data.SetIndexBufferParams(ogMaxIndicesCnt, IndexFormat.UInt32);
            indices.GetSubArray(0, ogMaxIndicesCnt).CopyTo(data.GetIndexData<int>());

            data.subMeshCount = 1;
            data.SetSubMesh(0, new SubMeshDescriptor {
                indexStart = 0,
                indexCount = ogMaxIndicesCnt,
                topology = MeshTopology.Triangles,
            }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
        }
    }

    [BurstCompile]
    public struct SetSkirtMeshDataJob : IJob {
        [WriteOnly]
        public Mesh.MeshData data;

        [ReadOnly]
        public NativeArray<VertexAttributeDescriptor> vertexAttributeDescriptors;
        
        [ReadOnly]
        public NativeArray<float3> skirtVertices;
        [ReadOnly]
        public NativeArray<float3> skirtNormals;
        [ReadOnly]
        public NativeArray<int> skirtForcedPerFaceIndices;

        [ReadOnly]
        public NativeCounter skirtVertexCounter;

        [ReadOnly]
        public NativeMultiCounter skirtForcedTriangleCounter;

        public void Execute() {
            /*
            int maxSkirtVerticesCnt = skirtVertexCounter.Count;
            data.SetVertexBufferParams(maxSkirtVerticesCnt, vertexAttributeDescriptors);

            skirtVertices.GetSubArray(0, maxSkirtVerticesCnt).CopyTo(data.GetVertexData<float3>(0));
            skirtNormals.GetSubArray(0, maxSkirtVerticesCnt).CopyTo(data.GetVertexData<float3>(1));
            skirtUvs.GetSubArray(0, maxSkirtVerticesCnt).CopyTo(data.GetVertexData<float2>(2));

            NativeArray<int> indexStarts = new NativeArray<int>(6, Allocator.Temp);
            NativeArray<int> indexCounts = new NativeArray<int>(6, Allocator.Temp);


            int baseSkirtIndexCount = skirtStitchedTriangleCounter.Count * 3;
            int totalIndices = baseSkirtIndexCount;

            for (int i = 0; i < 6; i++) {
                int cnt = skirtForcedTriangleCounter[i] * 3;
                indexStarts[i] = totalIndices;
                indexCounts[i] = cnt;
                totalIndices += cnt;
            }

            data.SetIndexBufferParams(totalIndices, IndexFormat.UInt32);

            // Copy the indices for the stitched mesh (submesh=0). Always will be enabled and visible
            NativeArray<int> indexData = data.GetIndexData<int>();
            NativeArray<int> dst = indexData.GetSubArray(0, baseSkirtIndexCount);
            NativeArray<int> src = skirtStitchedIndices.GetSubArray(0, baseSkirtIndexCount);
            src.CopyTo(dst);

            // Copy the triangles for each face
            for (int i = 0; i < 6; i++) {
                dst = indexData.GetSubArray(indexStarts[i], indexCounts[i]);
                src = skirtForcedPerFaceIndices.GetSubArray(VoxelUtils.SKIRT_FACE * i * 6, indexCounts[i]);
                src.CopyTo(dst);
            }

            // Set the main skirt submesh 
            data.subMeshCount = 7;
            data.SetSubMesh(0, new SubMeshDescriptor {
                indexStart = 0,
                indexCount = baseSkirtIndexCount,
                topology = MeshTopology.Triangles,
            }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            // Set the submeshes desu
            for (int i = 0; i < 6; i++) {
                data.SetSubMesh(i + 1, new SubMeshDescriptor {
                    indexStart = indexStarts[i],
                    indexCount = indexCounts[i],
                    topology = MeshTopology.Triangles,
                }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            }

            indexStarts.Dispose();
            indexCounts.Dispose();
            */
        }
    }
}