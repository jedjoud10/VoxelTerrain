using System.Linq;
using jedjoud.VoxelTerrain.Generation;
using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Segments {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateAfter(typeof(SegmentManagerSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial class SegmentVoxelSystem : SystemBase {
        public SegmentExecutor executor;
        public Entity entity;
        public TerrainSegment segment;
        public GraphicsFence fence;

        protected override void OnCreate() {
            RequireForUpdate<TerrainReadySystems>();
            executor = new SegmentExecutor();
        }

        protected override void OnUpdate() {
            RefRW<TerrainReadySystems> _ready = SystemAPI.GetSingletonRW<TerrainReadySystems>();
            _ready.ValueRW.segmentVoxels = true;

            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainSegment, TerrainSegmentRequestVoxelsTag>().Build();
            
            if (query.IsEmpty || !_ready.ValueRO.segmentProps) {
                entity = Entity.Null;
                segment = default;
                fence = default;
                return;
            }

            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            NativeArray<TerrainSegment> segments = query.ToComponentDataArray<TerrainSegment>(Allocator.Temp);
            entity = entities[0];
            segment = segments[0];

            fence = executor.Execute(new SegmentExecutorParameters() {
                commandBufferName = "Terrain Segment Voxels Dispatch",
                kernelName = "CSVoxels",
                updateInjected = false,
                compiler = ManagedTerrain.instance.compiler,
                seeder = ManagedTerrain.instance.seeder,
                position = segment.position,
            });

            SystemAPI.SetComponentEnabled<TerrainSegmentRequestVoxelsTag>(entity, false);
        }

        protected override void OnDestroy() {
            executor.DisposeResources();
        }
    }
}