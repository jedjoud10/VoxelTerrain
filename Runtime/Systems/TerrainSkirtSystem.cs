using jedjoud.VoxelTerrain.Generation;
using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    public partial struct TerrainSkirtSystem : ISystem {


        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            EntityQuery query = SystemAPI.QueryBuilder().WithAll<TerrainSkirtTag, LocalToWorld, MaterialMeshInfo>().Build();
            state.RequireForUpdate(query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            foreach (var chunk in SystemAPI.Query<TerrainChunk>()) {
                BitField32 skirtMask = chunk.skirtMask;

                if (chunk.skirts.Length == 7) {
                    for (int i = 0; i < 6; i++) {
                        // first skirt entity is used for stitching, must always be enabled
                        Entity skirtEntity = chunk.skirts[i + 1];

                        if (SystemAPI.HasComponent<MaterialMeshInfo>(skirtEntity)) {
                            SystemAPI.SetComponentEnabled<MaterialMeshInfo>(skirtEntity, skirtMask.IsSet(i));
                        }
                    }
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
        }
    }
}