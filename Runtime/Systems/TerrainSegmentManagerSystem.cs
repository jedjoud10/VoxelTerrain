using System.Linq;
using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain.Segments {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TerrainSegmentManagerSystem : ISystem {
        private NativeHashMap<TerrainSegment, Entity> map;
        private NativeHashSet<TerrainSegment> oldSegments;
        private NativeHashSet<TerrainSegment> newSegments;
        private NativeList<TerrainSegment> addedSegments;
        private NativeList<TerrainSegment> removedSegments;
        private JobHandle handle;
        private Entity segmentPrototype;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainLoader>();
            state.RequireForUpdate<TerrainOctreeConfig>();
            oldSegments = new NativeHashSet<TerrainSegment>(0, Allocator.Persistent);
            newSegments = new NativeHashSet<TerrainSegment>(0, Allocator.Persistent);
            addedSegments = new NativeList<TerrainSegment>(Allocator.Persistent);
            removedSegments = new NativeList<TerrainSegment>(Allocator.Persistent);
            map = new NativeHashMap<TerrainSegment, Entity>(0, Allocator.Persistent);


            EntityManager mgr = state.EntityManager;
            segmentPrototype = mgr.CreateEntity();
            mgr.AddComponent<LocalToWorld>(segmentPrototype);
            mgr.AddComponent<TerrainSegment>(segmentPrototype);
            mgr.AddComponent<Prefab>(segmentPrototype);
            mgr.AddComponent<TerrainSegmentRequestDispatchTag>(segmentPrototype);
            mgr.SetComponentEnabled<TerrainSegmentRequestDispatchTag>(segmentPrototype, true);
        }

        [BurstCompile]
        private void Complete(ref SystemState state) {
            handle.Complete();


            foreach (var segment in removedSegments) {
                if (map.TryGetValue(segment, out Entity entity)) {
                    state.EntityManager.DestroyEntity(entity);
                    map.Remove(segment);
                }
            }

            foreach (var segment in addedSegments) {
                Entity entity = state.EntityManager.Instantiate(segmentPrototype);
                map.Add(segment, entity);

                float4x4 matrix = float4x4.Translate((float3)segment.position * SegmentUtils.WORLD_SEGMENT_SIZE);
                SystemAPI.SetComponent<LocalToWorld>(entity, new LocalToWorld {
                    Value = matrix
                });
            }
        }

        [BurstCompile]
        private void Schedule(ref SystemState state) {
            Entity entity = SystemAPI.GetSingletonEntity<TerrainLoader>();
            TerrainLoader loader = SystemAPI.GetComponent<TerrainLoader>(entity);
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(entity);
            TerrainOctreeConfig config = SystemAPI.GetSingleton<TerrainOctreeConfig>();

            OctreeNode root = OctreeNode.RootNode(config.maxDepth, VoxelUtils.PHYSICAL_CHUNK_SIZE /* >> (int)terrain.voxelSizeReduction */);
            int maxSegmentsInWorld = root.size / SegmentUtils.WORLD_SEGMENT_SIZE;

            SegmentSpawnJob job = new SegmentSpawnJob {
                addedSegments = addedSegments,
                removedSegments = removedSegments,

                center = transform.Position,
                extent = loader.segmentExtent,

                newSegments = newSegments,
                oldSegments = oldSegments,

                lodMultiplier = loader.segmentLodFactor,

                maxSegmentsInWorld = maxSegmentsInWorld,
                worldSegmentSize = SegmentUtils.WORLD_SEGMENT_SIZE,
            };
            handle = job.Schedule();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            if (!handle.IsCompleted)
                return;

            Complete(ref state);
            Schedule(ref state);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            handle.Complete();
            oldSegments.Dispose();
            newSegments.Dispose();
            addedSegments.Dispose();
            removedSegments.Dispose();
            map.Dispose();
        }
    }
}