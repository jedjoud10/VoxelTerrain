using jedjoud.VoxelTerrain.Generation;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Segments {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateAfter(typeof(TerrainSegmentManagerSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial class TerrainSegmentVoxelSystem : SystemBase {
        public SegmentVoxelExecutor voxelExecutor;
        public Entity entity;
        public TerrainSegment segment;
        public GraphicsFence fence;

        protected override void OnCreate() {
            RequireForUpdate<TerrainReadySystems>();
            voxelExecutor = new SegmentVoxelExecutor();
        }

        protected override void OnUpdate() {
            RefRW<TerrainReadySystems> _ready = SystemAPI.GetSingletonRW<TerrainReadySystems>();
            _ready.ValueRW.segmentVoxels = true;

            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainSegment, TerrainSegmentRequestVoxelsTag>().Build();
            
            if (query.IsEmpty || !_ready.ValueRO.segmentPropsDispatch) {
                entity = Entity.Null;
                segment = default;
                fence = default;
                return;
            }

            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            NativeArray<TerrainSegment> segments = query.ToComponentDataArray<TerrainSegment>(Allocator.Temp);
            entity = entities[0];
            segment = segments[0];

            fence = voxelExecutor.Execute(new SegmentVoxelExecutorParameters() {
                commandBufferName = "Terrain Segment Voxels Dispatch",
                kernelName = "CSVoxels",
                updateInjected = false,
                compiler = ManagedTerrain.instance.compiler,
                segment = segment,
            });

            SystemAPI.SetComponentEnabled<TerrainSegmentRequestVoxelsTag>(entity, false);
        }

        protected override void OnDestroy() {
            voxelExecutor.DisposeResources();
        }
    }
}