using jedjoud.VoxelTerrain.Octree;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

namespace jedjoud.VoxelTerrain.Edits {
    [UpdateInGroup(typeof(TerrainFixedStepSystemGroup))]
    [UpdateAfter(typeof(TerrainOctreeSystem))]
    [UpdateBefore(typeof(TerrainManagerSystem))]
    [UpdateAfter(typeof(TerrainEditIncrementalModifySystem))]
    [UpdateBefore(typeof(TerrainEditRefreshChunksSystem))]
    public partial struct TerrainEditBootstrapDefaultsSystem : ISystem {
        EditUtils.BootstrappedEditStorage<TerrainAddEdit> addEdits;
        EditUtils.BootstrappedEditStorage<TerrainSphereEdit> sphereEdits;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<TerrainEdits>();
            state.RequireForUpdate<TerrainEdit>();
            addEdits = new EditUtils.BootstrappedEditStorage<TerrainAddEdit>(ref state);
            sphereEdits = new EditUtils.BootstrappedEditStorage<TerrainSphereEdit>(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            TerrainEdits backing = SystemAPI.GetSingleton<TerrainEdits>();
            addEdits.Update(ref state, backing);
            sphereEdits.Update(ref state, backing);
        }
    }
}