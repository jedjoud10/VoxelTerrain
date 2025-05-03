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
        public NativeArray<float3> vertices;
        public NativeArray<float3> normals;
        public NativeArray<int> triangles;

        // LOD1 indices
        public NativeArray<int> lod1Indices;

        // LOD0 indices (we have 4 neighbours so 4)
        public NativeArray<int>[] lod0Indices;

        // TODO: add multi-material support later
        public Unsafe.NativeCounter quads;
        public Unsafe.NativeCounter counter;
        public VoxelMesher mesher;
        internal NativeArray<VertexAttributeDescriptor> vertexAttributeDescriptors;

        internal StitchJobHandler(VoxelMesher mesher) {
            this.mesher = mesher;

            // We generate at most 64x64x2 LOD1 vertices (one slice for dupe, one slice for the downsampled ones)
            // We generate at most 128x128 LOD0 vertices (one slice for dupe)
            int maxVerts = VoxelUtils.SIZE * VoxelUtils.SIZE * 2 + VoxelUtils.SIZE * VoxelUtils.SIZE * 4;

            int maxTris = maxVerts * 3; // kinda dumb but wtv

            // Native buffers for mesh data
            vertices = new NativeArray<float3>(maxVerts, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            triangles = new NativeArray<int>(maxTris, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            lod1Indices = new NativeArray<int>(VoxelUtils.SIZE * VoxelUtils.SIZE * 2, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            lod0Indices = new NativeArray<int>[4];

            for (int i = 0; i < 4; i++) {
                lod0Indices[i] = new NativeArray<int>(VoxelUtils.SIZE * VoxelUtils.SIZE, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            quads = new Unsafe.NativeCounter(Allocator.Persistent);
            counter = new Unsafe.NativeCounter(Allocator.Persistent);

            VertexAttributeDescriptor positionDesc = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
            vertexAttributeDescriptors = new NativeArray<VertexAttributeDescriptor>(new VertexAttributeDescriptor[] { positionDesc, }, Allocator.Persistent);
        }

        // Raw dogging it...
        internal void DoThingyMajig(VoxelMesher.StitchingRequest req, VoxelChunk lod1, VoxelStitch stitch) {
            counter.Count = 0;
            quads.Count = 0;

            // mesh extra vertices in the positive X direction in LOD1
            // we also need to re-generate the old vertices since this is in a new mesh, so it's actually gonna be a 2x64x64 job
            CreatePaddingVerticesLod1Job createDuplicateVertsAndPaddingVertsLod1Job = new CreatePaddingVerticesLod1Job() {
                voxels = lod1.voxels,
                counter = counter,
                indices = lod1Indices,
                paddingBlurredFaceVoxels = lod1.blurredPositiveXFacingExtraVoxelsFlat,
                vertices = vertices
            };

            // we could just read them back from some cached state (at least for the vertices at slice=0) , but that would introduce chunk mesh dependency (we'd need to wait for the chunk to finish meshing first)
            createDuplicateVertsAndPaddingVertsLod1Job.Schedule(VoxelUtils.SIZE * VoxelUtils.SIZE * 2, 1024).Complete();

            // create the quads from the duped vertices and extra ones in LOD1
            Lod1QuadJob lod1QuadJob = new Lod1QuadJob {
                counter = quads,
                indices = lod1Indices,
                triangles = triangles,
                voxels = lod1.voxels,
                paddingBlurredFaceVoxels = lod1.blurredPositiveXFacingExtraVoxelsFlat,
            };
            lod1QuadJob.Schedule(VoxelUtils.SIZE * VoxelUtils.SIZE, 1024).Complete();

            // duplicate the vertices from the LOD0 chunks
            // we could just read them back from some cached state, but that would introduce chunk mesh dependency (we'd need to wait for the chunk to finish meshing first)
            for (int i = 0; i < 4; i++) {
                DuplicateLod0VerticesJob createDuplicateVertsLod0 = new DuplicateLod0VerticesJob() {
                    voxels = stitch.lod0Neighbours[i].voxels,
                    vertices = vertices,
                    counter = counter,
                    indices = lod0Indices[i],
                    relativeOffsetToLod1 = VoxelUtils.IndexToPosMorton2D(i),
                };

                createDuplicateVertsLod0.Schedule(VoxelUtils.SIZE * VoxelUtils.SIZE, 1024).Complete();
            }

            // actual stitching jobs that will be executed at a higher resolution
            for (int i = 0; i < 4; i++) {
                StitchQuadJob stitchJob = new StitchQuadJob() {
                    counter = quads,
                    lod0indices = lod0Indices[i],
                    lod1indices = lod1Indices,
                    triangles = triangles,
                    voxels = stitch.lod0Neighbours[i].voxels,
                    relativeOffsetToLod1 = VoxelUtils.IndexToPosMorton2D(i),
                };

                stitchJob.Schedule(VoxelUtils.SIZE * VoxelUtils.SIZE, 1024).Complete();
            }

            MeshFilter filter = stitch.GetComponent<MeshFilter>();
            Mesh mesh = new Mesh();
            stitch.vertices = vertices.Reinterpret<Vector3>().Slice(0, counter.Count).ToArray();
            stitch.triangles = triangles.Slice(0, quads.Count * 6).ToArray();
            mesh.vertices = stitch.vertices;
            mesh.triangles = stitch.triangles;
            filter.mesh = mesh;


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
            lod1Indices.Dispose();

            foreach (var item in lod0Indices) {
                item.Dispose();
            }
            
            vertices.Dispose();
            normals.Dispose();
            counter.Dispose();
            quads.Dispose();
            triangles.Dispose();
            vertexAttributeDescriptors.Dispose();
        }
    }
}