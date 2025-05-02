using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Meshing {
    /*
    goes from higher res to lower res
    def higher res = LOD0
    def lower res = LOD1
    assuming 2:1 ratio
    
    first makes an intermediate layer of voxels that use lower res data but stored at a higher res (upsampled)
    then do extra meshing for LOD0 using that upsampled data
    then we do the stitching based on vertex indices in LOD1 and connect them to LOD0
    
    there are 2 scenarios possible:
    1) LOD1 chunk is missing the last data points in the positive axii (+x, +y, +z) (BIG gap between LOD0 LOD1):
      steps:
      get the LOD0 neighbours and fetch their facet voxels
      downsample (blur) 2x2x2 regions from LOD0 facet voxels. 
      create extra vertex using blurred data and old LOD1 data
      fetch downsampled data back in LOD0, but upsample it this time (makes sure that the two LODs share a common ground; low-res data)
    2) LOD0 chunk is missing the last data points in the positive axii (+x, +y, +z)
      steps:
      avoid generating vertices (63x63x63) in the positive (+x,+y,+z) directions
      do the same 1) replace the last voxel values with downsampled data instead
    */

    internal class StitchJobHandler {
        // Voxel data of the LOD1 chunk face (low-res)
        public NativeArray<Voxel> lod1Voxels;

        // Voxel data of the LOD0 chunk face (high-res)
        // We will do some downsampling / blurring on this shit... ts pmo
        public NativeArray<Voxel> lod0Voxels;

        public NativeArray<float3> vertices;
        public NativeArray<float3> normals;
        public NativeArray<int> triangles;
        public NativeArray<int> indices;

        // TODO: add multi-material support later
        public Unsafe.NativeCounter quads;
        public Unsafe.NativeCounter counter;
        public VoxelMesher mesher;
        internal NativeArray<VertexAttributeDescriptor> vertexAttributeDescriptors;

        internal StitchJobHandler(VoxelMesher mesher) {
            this.mesher = mesher;

            // In worst case scenarios
            int maxVerts = (VoxelUtils.SIZE + 1) * (VoxelUtils.SIZE + 1) * 2;
            int maxTris = maxVerts * 3; // kinda dumb but wtv

            // Native buffers for mesh data
            vertices = new NativeArray<float3>(maxVerts, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            triangles = new NativeArray<int>(maxTris, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            indices = new NativeArray<int>(VoxelUtils.VOLUME_BIG, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            quads = new Unsafe.NativeCounter(Allocator.Persistent);
            counter = new Unsafe.NativeCounter(Allocator.Persistent);

            VertexAttributeDescriptor positionDesc = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
            vertexAttributeDescriptors = new NativeArray<VertexAttributeDescriptor>(new VertexAttributeDescriptor[] { positionDesc, }, Allocator.Persistent);
        }

        // Raw dogging it...
        internal void DoThingyMajig(VoxelMesher.StitchingRequest req, VoxelChunk lod0, VoxelChunk lod1) {
            // calculate offset
            int quadrantVolume = VoxelUtils.SIZE * VoxelUtils.SIZE / 4;
            int quadrantSize = VoxelUtils.SIZE / 2;
            float3 srcPos = lod0.node.position;
            float3 dstPos = lod1.node.position;
            uint2 offset = (uint2)((srcPos - dstPos).yz / VoxelUtils.SIZE);
            lod0.relativeOffsetToLod1 = offset;

            /*
            if (!math.all(offset == new uint2(0,1))) {
                return;
            }
            */

            // fetch the voxels from the source chunk and blur them
            int mortonOffset = (int)Morton.EncodeMorton2D_32(offset) * quadrantVolume;
            //Debug.Log(mortonOffset);


            if (!lod1.blurredPositiveXFacingExtraVoxelsFlat.IsCreated) {
                lod1.blurredPositiveXFacingExtraVoxelsFlat = new NativeArray<Voxel>(VoxelUtils.SIZE * VoxelUtils.SIZE, Allocator.Persistent);
            }
            
            FaceVoxelsBlurJob copy = new FaceVoxelsBlurJob() {
                voxels = lod0.voxels,
                dstFace = lod1.blurredPositiveXFacingExtraVoxelsFlat,
                mortonOffset = mortonOffset,
            };

            // since we will be blurring each 2x2x2 region (from LOD0) into a single voxel (into LOD1) we will at max be writing to a single "quadrant" of the face
            copy.Schedule(quadrantVolume, 1024).Complete();

            // do some sort of meshing sheise that will use the new blurred data and the old data from lod1 but going INTO the negative direction (face direction)


            /*
            float voxelSizeFactor = mesher.terrain.voxelSizeFactor;
            countersQuad.Reset();
            counter.Count = 0;
            materialCounter.Count = 0;
            materialHashSet.Clear();
            materialHashMap.Clear();
            bounds[0] = new float3(VoxelUtils.SIZE * voxelSizeFactor);
            bounds[1] = new float3(0.0);
            Free = false;

            unsafe {
                neighbourPtrs.Clear();
                foreach (NativeArray<Voxel> v in neighboursArray) {
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
                neighbourMask = mask,
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
                neighbours = neighbourPtrs,
                neighbourMask = mask,
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
                neighbours = neighbourPtrs,
                neighbourMask = mask,
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
            JobHandle materialJobHandle = materialJob.Schedule(VoxelUtils.VOLUME_BIG, 2048 * 8 * INNER_LOOP_BATCH_COUNT, dependency);
            JobHandle materialIndexerJobHandle = materialIndexerJob.Schedule(materialJobHandle);

            // Start the corner job and material job
            JobHandle cornerJobHandle = cornerJob.Schedule(VoxelUtils.VOLUME_BIG, 2048 * INNER_LOOP_BATCH_COUNT, dependency);

            // Start the vertex job
            JobHandle vertexDep = JobHandle.CombineDependencies(cornerJobHandle, dependency);
            JobHandle vertexJobHandle = vertexJob.Schedule(VoxelUtils.VOLUME_BIG, 2048 * INNER_LOOP_BATCH_COUNT, vertexDep);
            JobHandle boundsJobHandle = boundsJob.Schedule(vertexJobHandle);
            JobHandle aoJobHandle = aoJob.Schedule(VoxelUtils.VOLUME_BIG, 2048 * INNER_LOOP_BATCH_COUNT, vertexJobHandle);

            // Start the quad job
            JobHandle merged = JobHandle.CombineDependencies(vertexJobHandle, cornerJobHandle, materialIndexerJobHandle);
            JobHandle quadJobHandle = quadJob.Schedule(VoxelUtils.VOLUME_BIG, 2048 * INNER_LOOP_BATCH_COUNT, merged);

            // Start the sum job 
            JobHandle sumJobHandle = sumJob.Schedule(quadJobHandle);

            // Start the copy job
            JobHandle copyJobHandle = copyJob.Schedule(VoxelUtils.MAX_MATERIAL_COUNT, 32, sumJobHandle);

            finalJobHandle = JobHandle.CombineDependencies(copyJobHandle, boundsJobHandle, aoJobHandle);
            */
        }

        // Dispose of the underlying memory allocations
        internal void Dispose() {
            indices.Dispose();
            vertices.Dispose();
            normals.Dispose();
            counter.Dispose();
            quads.Dispose();
            triangles.Dispose();
            vertexAttributeDescriptors.Dispose();
        }
    }
}