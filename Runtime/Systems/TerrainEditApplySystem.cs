using jedjoud.VoxelTerrain.Generation;
using jedjoud.VoxelTerrain.Meshing;
using jedjoud.VoxelTerrain.Octree;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

namespace jedjoud.VoxelTerrain.Edits {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateAfter(typeof(TerrainReadbackSystem))]
    [UpdateBefore(typeof(TerrainMeshingSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial class EditApplySystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<TerrainEdits>();
        }

        protected override void OnUpdate() {
            TerrainEdits backing = SystemAPI.ManagedAPI.GetSingleton<TerrainEdits>();

            NativeHashMap<int3, int> chunkPositionsToChunkEditIndices = backing.chunkPositionsToChunkEditIndices;
            UnsafePtrListVoxelData chunkEditsCopyRaw = new UnsafePtrListVoxelData(Allocator.Persistent);
            chunkEditsCopyRaw.AddReadOnlyRangePtrs(backing.chunkEdits);

            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainChunkVoxels, TerrainChunk>().WithAny<TerrainChunkRequestMeshingTag, TerrainChunkRequestReadbackTag>().Build();
            NativeArray<TerrainChunk> chunks = query.ToComponentDataArray<TerrainChunk>(Allocator.Temp);
            NativeArray<Entity> chunkEntities = query.ToEntityArray(Allocator.Temp);
            NativeHashSet<Entity> modifiedChunkEntities = new NativeHashSet<Entity>(0, Allocator.Temp);

            // loop over all the chunks that are going to be meshed and check if we need to inject the custom edit data into them
            // stupid double for loop, should work tho
            NativeArray<int3> editChunkPositions = chunkPositionsToChunkEditIndices.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < chunks.Length; i++) {
                MinMaxAABB chunkBounds = chunks[i].node.Bounds;

                foreach (var editChunkPosition in editChunkPositions) {
                    float3 min = editChunkPosition * VoxelUtils.PHYSICAL_CHUNK_SIZE;
                    float3 max = min + VoxelUtils.PHYSICAL_CHUNK_SIZE;
                    MinMaxAABB editChunkBounds = new MinMaxAABB(min, max);

                    if (chunkBounds.Overlaps(editChunkBounds))
                        modifiedChunkEntities.Add(chunkEntities[i]);
                }
            }

            NativeList<JobHandle> allDependencies = new NativeList<JobHandle>(Allocator.Temp);

            foreach (var entity in modifiedChunkEntities) {
                OctreeNode node = SystemAPI.GetComponent<TerrainChunk>(entity).node;

                if (SystemAPI.IsComponentEnabled<TerrainChunkVoxelsReadyTag>(entity) && SystemAPI.IsComponentEnabled<TerrainChunkRequestMeshingTag>(entity)) {
                    ref TerrainChunkVoxels voxels = ref SystemAPI.GetComponentRW<TerrainChunkVoxels>(entity).ValueRW;
                    voxels.asyncWriteJobHandle.Complete();

                    EditApplyJob job = new EditApplyJob {
                        chunkPositionsToChunkEditIndices = chunkPositionsToChunkEditIndices,
                        chunkEditsRaw = chunkEditsCopyRaw,

                        chunkScale = node.size / VoxelUtils.PHYSICAL_CHUNK_SIZE,
                        chunkOffset = node.position,

                        voxels = voxels.data
                    };

                    JobHandle dep = JobHandle.CombineDependencies(voxels.asyncReadJobHandle, voxels.asyncWriteJobHandle);
                    JobHandle handle = job.Schedule(VoxelUtils.VOLUME, BatchUtils.SMALLEST_BATCH, dep);
                    voxels.asyncWriteJobHandle = handle;
                    allDependencies.Add(handle);
                    SystemAPI.GetComponentRW<TerrainChunkRequestMeshingTag>(entity).ValueRW.deferredVisibility = false;
                }

                if (SystemAPI.HasComponent<TerrainChunkRequestReadbackTag>(entity)) {
                    SystemAPI.GetComponentRW<TerrainChunkRequestReadbackTag>(entity).ValueRW.skipMeshingIfEmpty = false;
                }
            }

            JobHandle final = JobHandle.CombineDependencies(allDependencies.AsArray());
            chunkEditsCopyRaw.Dispose(final);

            JobHandle finalPlusLast = JobHandle.CombineDependencies(backing.applySystemHandle, final);
            backing.applySystemHandle = finalPlusLast;
        }
    }
}