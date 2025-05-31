using System.Linq;
using Codice.Client.BaseCommands.BranchExplorer.ExplorerTree;
using jedjoud.VoxelTerrain.Generation;
using jedjoud.VoxelTerrain.Props;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Segments {
    [UpdateInGroup(typeof(FixedStepTerrainSystemGroup))]
    [UpdateAfter(typeof(SegmentVoxelSystem))]
    public partial class SegmentPropStuffSystem : SystemBase {
        private bool initialized;
        
        protected override void OnCreate() {
            RequireForUpdate<TerrainReadySystems>();
            RequireForUpdate<TerrainPropsConfig>();

            initialized = false;
        }

        protected override void OnUpdate() {
            TerrainPropsConfig config = SystemAPI.ManagedAPI.GetSingleton<TerrainPropsConfig>();
            
            if (!initialized) {
                initialized = true;
                TerrainPropStuff stuff = new TerrainPropStuff();
                stuff.Init(config);
                EntityManager.CreateSingleton<TerrainPropStuff>(stuff);
            }
        }

        protected override void OnDestroy() {
            AsyncGPUReadback.WaitAllRequests();

            if (initialized) {
                SystemAPI.ManagedAPI.GetSingleton<TerrainPropStuff>().Dispose();
            }
        }
    }
}