using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace jedjoud.VoxelTerrain.Segments {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TerrainSegmentManagerSystem : ISystem {
        private NativeHashMap<TerrainSegment, Entity> map;
        private NativeHashSet<TerrainSegment> oldSegments;
        private NativeHashSet<TerrainSegment> newSegments;
        private NativeList<TerrainSegment> addedSegments;
        private NativeList<TerrainSegment> removedSegments;
        private JobHandle handle;
        private Entity segmentPrototype;

        private NativeList<Entity> segmentsThatMustBeInEndOfPipe;
        private NativeList<Entity> segmentsToDestroy;

        private NativeList<TerrainLoader> loaders;

        private bool pending;


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
            mgr.AddComponent<TerrainSegmentRequestPropsTag>(segmentPrototype);
            mgr.AddComponent<TerrainSegmentRequestVoxelsTag>(segmentPrototype);
            mgr.AddComponent<TerrainSegmentEndOfPipeTag>(segmentPrototype);
            mgr.SetComponentEnabled<TerrainSegmentRequestPropsTag>(segmentPrototype, true);
            mgr.SetComponentEnabled<TerrainSegmentRequestVoxelsTag>(segmentPrototype, true);
            mgr.SetComponentEnabled<TerrainSegmentEndOfPipeTag>(segmentPrototype, false);
            mgr.AddComponent<Prefab>(segmentPrototype);

            segmentsThatMustBeInEndOfPipe = new NativeList<Entity>(Allocator.Persistent);
            segmentsToDestroy = new NativeList<Entity>(Allocator.Persistent);
            pending = false;

            loaders = new NativeList<TerrainLoader>(Allocator.Persistent);
        }

        [BurstCompile]
        private void Complete(ref SystemState state) {
            handle.Complete();

            foreach (var segment in removedSegments) {
                if (map.TryGetValue(segment, out Entity entity)) {
                    segmentsToDestroy.Add(entity);
                }
            }

            foreach (var segment in addedSegments) {
                Entity entity = state.EntityManager.Instantiate(segmentPrototype);
                map.Add(segment, entity);

                float4x4 matrix = float4x4.Translate((float3)segment.position * SegmentUtils.PHYSICAL_SEGMENT_SIZE);
                state.EntityManager.SetComponentData<LocalToWorld>(entity, new LocalToWorld {
                    Value = matrix
                });

                state.EntityManager.SetComponentData<TerrainSegment>(entity, segment);
                segmentsThatMustBeInEndOfPipe.Add(entity);
            }

            pending = false;
        }

        [BurstCompile]
        private void Schedule(ref SystemState state) {
            TerrainOctreeConfig config = SystemAPI.GetSingleton<TerrainOctreeConfig>();

            OctreeNode root = OctreeNode.RootNode(config.maxDepth, VoxelUtils.PHYSICAL_CHUNK_SIZE /* >> (int)terrain.voxelSizeReduction */);
            int maxSegmentsInWorld = (root.size / SegmentUtils.PHYSICAL_SEGMENT_SIZE) / 2;

            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainLoader>().Build();
            NativeArray<TerrainLoader> tempLoaders = query.ToComponentDataArray<TerrainLoader>(Allocator.Temp);
            loaders.CopyFrom(tempLoaders);

            SegmentSpawnJob job = new SegmentSpawnJob {
                addedSegments = addedSegments,
                removedSegments = removedSegments,

                loaders = loaders,

                newSegments = newSegments,
                oldSegments = oldSegments,


                maxSegmentsInWorld = maxSegmentsInWorld,
                worldSegmentSize = SegmentUtils.PHYSICAL_SEGMENT_SIZE,
            };
            handle = job.Schedule();
            pending = true;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            if (handle.IsCompleted && pending) {
                Complete(ref state);
            } else if (segmentsToDestroy.Length == 0 && segmentsThatMustBeInEndOfPipe.Length == 0 && !pending) {
                Schedule(ref state);
            }

            bool areAllInEoP = true;

            foreach (var item in segmentsThatMustBeInEndOfPipe) {
                areAllInEoP &= SystemAPI.IsComponentEnabled<TerrainSegmentEndOfPipeTag>(item);
            }

            RefRW<TerrainReadySystems> _ready = SystemAPI.GetSingletonRW<TerrainReadySystems>();
            _ready.ValueRW.segmentManager = segmentsToDestroy.Length == 0 && segmentsThatMustBeInEndOfPipe.Length == 0 && areAllInEoP && !pending;

            if (areAllInEoP && segmentsThatMustBeInEndOfPipe.Length > 0) {
                foreach (var entity in segmentsToDestroy) {
                    TerrainSegment segment = state.EntityManager.GetComponentData<TerrainSegment>(entity);
                    state.EntityManager.AddComponentData<TerrainSegmentPendingRemoval>(entity, new TerrainSegmentPendingRemoval {
                        propsNeedCleanup = false
                    });
                    map.Remove(segment);
                }

                segmentsToDestroy.Clear();
                segmentsThatMustBeInEndOfPipe.Clear();
            }

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (segment, entity) in SystemAPI.Query<TerrainSegmentPendingRemoval>().WithEntityAccess()) {
                if (segment.propsNeedCleanup) {
                    ecb.DestroyEntity(entity);
                }
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
            handle.Complete();
            oldSegments.Dispose();
            newSegments.Dispose();
            addedSegments.Dispose();
            removedSegments.Dispose();
            map.Dispose();
            segmentsToDestroy.Dispose();
            segmentsThatMustBeInEndOfPipe.Dispose();
            loaders.Dispose();
        }
    }
}