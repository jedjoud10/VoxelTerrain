using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static jedjoud.VoxelTerrain.VoxelUtils;
using static jedjoud.VoxelTerrain.BatchUtils;

namespace jedjoud.VoxelTerrain.Meshing {
    internal struct SkirtSnHandler : ISubHandler {
        public Vertices skirtVertices;
        public NativeCounter skirtVertexCounter;

        public NativeArray<bool> skirtWithinThreshold;
        public NativeArray<int> skirtVertexIndicesCopied;
        public NativeArray<int> skirtVertexIndicesGenerated;
        public NativeArray<int> skirtStitchedIndices;
        public NativeArray<int> skirtForcedPerFaceIndices;

        public NativeCounter skirtStitchedTriangleCounter;
        public NativeMultiCounter skirtForcedTriangleCounter;

        public JobHandle skirtVertexJobHandle;
        public JobHandle skirtQuadJobHandle;

        public void Init() {
            skirtVertices = new Vertices(SKIRT_FACE * 6, Allocator.Persistent);
            skirtVertexCounter = new NativeCounter(Allocator.Persistent);

            // Dedicated vertex index lookup buffers for the copied vertices from the boundary and generated skirted vertices
            skirtVertexIndicesCopied = new NativeArray<int>(FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            skirtVertexIndicesGenerated = new NativeArray<int>(SKIRT_FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // Stored sequentially, used for the main skirt mesh (submesh = 0)
            // Uses the skirtStitchedIndexCounter as next ptr
            // Since there can be 2 quads in each perpendicular direction, we must multiply by 2 desu
            // Uses indices that refer to vertices stored in the main mesh NOT THE COPIED VERTICES!!!!
            skirtStitchedIndices = new NativeArray<int>(SKIRT_FACE * 2 * 6 * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // Stored with gaps, since it will be copied to the submeshes' triangle array at an offset
            // Each face can reserve up to VoxelUtils.SKIRT_FACE * 6 indices for itself, so since we have 6 faces, we mult by 6
            skirtForcedPerFaceIndices = new NativeArray<int>(SKIRT_FACE * 6 * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            skirtStitchedTriangleCounter = new NativeCounter(Allocator.Persistent);
            skirtForcedTriangleCounter = new NativeMultiCounter(6, Allocator.Persistent);

            skirtWithinThreshold = new NativeArray<bool>(FACE * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        public void Schedule(ref VoxelData voxels, ref NormalsHandler normals, ref CoreSnHandler core, JobHandle dependency) {
            skirtStitchedTriangleCounter.Count = 0;
            skirtForcedTriangleCounter.Reset();
            skirtVertexCounter.Count = 0;

            // Job that acts like an SDF generator, checks if certain positions are within a certain distance from a surface (for forced skirt generation)
            SkirtClosestSurfaceJob skirtClosestSurfaceThresholdJob = new SkirtClosestSurfaceJob {
                voxels = voxels,
                withinThreshold = skirtWithinThreshold,
            };

            // Create a copy job that will copy the boundary indices of the original mesh (needed for skirt quad job)
            SkirtCopyVertexIndicesJob skirtCopyVertexIndicesJob = new SkirtCopyVertexIndicesJob {
                skirtVertexIndicesCopied = skirtVertexIndicesCopied,
                sourceVertexIndices = core.vertexIndices,
            };

            // Create the skirt vertices in one of the chunk's face
            SkirtVertexJob skirtVertexJob = new SkirtVertexJob {
                skirtVertexIndicesGenerated = skirtVertexIndicesGenerated,
                skirtVertices = skirtVertices,
                withinThreshold = skirtWithinThreshold,
                voxels = voxels,
                voxelNormals = normals.voxelNormals,

                skirtVertexCounter = skirtVertexCounter,
                
                // READ ONLY!!!!! used as an offset desu
                vertexCounter = core.vertexCounter,
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
            };

            // Keep track of the voxels that are near the surface (does a 5x5 box-blur like lookup in 2D to check for surface)
            JobHandle closestSurfaceJobHandle = skirtClosestSurfaceThresholdJob.Schedule(FACE * 6, SMALLER_SKIRT_BATCH, dependency);

            // Copies vertex indices from the boundary in the source mesh to our skirt vertices. We only need to copy indices since we are using submeshes for our skirts, so they all share the same vertex buffer
            JobHandle skirtCopyJobHandle = skirtCopyVertexIndicesJob.Schedule(core.vertexJobHandle);

            // Creates skirt vertices (both normal and forced). needs to run at VoxelUtils.SKIRT_FACE since it has a padding of 2 (for edge case on the boundaries)
            skirtVertexJobHandle = skirtVertexJob.Schedule(SKIRT_FACE * 6, PER_SKIRT_FACE_SMALLER_BATCH, JobHandle.CombineDependencies(skirtCopyJobHandle, closestSurfaceJobHandle));

            // Creates quad based on the copied vertices and skirt-generated vertices
            skirtQuadJobHandle = skirtQuadJob.Schedule(FACE * 6, PER_SKIRT_FACE_SMALLER_BATCH, skirtVertexJobHandle);
        }

        public void Dispose() {
            skirtVertexIndicesCopied.Dispose();
            skirtVertexIndicesGenerated.Dispose();
            skirtForcedTriangleCounter.Dispose();
            skirtStitchedTriangleCounter.Dispose();
            skirtForcedPerFaceIndices.Dispose();
            skirtStitchedIndices.Dispose();
            skirtWithinThreshold.Dispose();

            skirtVertices.Dispose();
            skirtVertexCounter.Dispose();
        }
    }
}