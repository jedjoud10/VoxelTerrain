using System.Linq;
using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Segments {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial class TerrainSegmentDispatchSystem : SystemBase {

        protected override void OnCreate() {
        }

        protected override void OnUpdate() {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainSegment, TerrainSegmentRequestDispatchTag>().Build();
            
            if (query.IsEmpty) {
                return;
            }

            NativeArray<Entity> entitiesArray = query.ToEntityArray(Allocator.Temp);
            Entity entity = entitiesArray[1];
            SystemAPI.SetComponentEnabled<TerrainSegmentRequestDispatchTag>(entity, false);
        }
    }
}