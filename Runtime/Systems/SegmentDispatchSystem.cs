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
    public partial class SegmentDispatchSystem : SystemBase {
        private SimpleExecutor executor;

        protected override void OnCreate() {
            executor = new SimpleExecutor(SegmentUtils.SEGMENT_SIZE);
        }

        protected override void OnUpdate() {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainSegment, TerrainSegmentRequestDispatchTag>().Build();
            
            if (query.IsEmpty) {
                return;
            }

            NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            NativeArray<TerrainSegment> segments = query.ToComponentDataArray<TerrainSegment>(Allocator.Temp);
            Entity entity = entities[0];
            TerrainSegment segment = segments[0];

            /*
            SimpleExecutorParameters parameters = new SimpleExecutorParameters() {
                commandBufferName = "Terrain Segment Dispatch System Async Dispatch",
                dispatchName = "voxels",
                updateInjected = false,
                compiler = ManagedTerrain.instance.compiler,
                seeder = ManagedTerrain.instance.seeder,
                
                scale = SegmentUtils.CHUNK_TO_SEGMENT_SIZE_RATIO * Vector3.one,
                offset = (float3)segment.position * SegmentUtils.PHYSICAL_SEGMENT_SIZE * Vector3.one,
            };

            GraphicsFence fence = executor.Execute(parameters);
            */

            SystemAPI.SetComponentEnabled<TerrainSegmentRequestDispatchTag>(entity, false);
        }
    }
}