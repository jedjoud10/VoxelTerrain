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
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
    public partial class SegmentPropsRenderSystem : SystemBase {

        protected override void OnCreate() {
            RequireForUpdate<TerrainReadySystems>();
            RequireForUpdate<TerrainPropsConfig>();
            RequireForUpdate<TerrainPropStuff>();
        }


        protected override void OnUpdate() {
            var config = SystemAPI.ManagedAPI.GetSingleton<TerrainPropsConfig>();
            var stuff = SystemAPI.ManagedAPI.GetSingleton<TerrainPropStuff>();
            stuff.RenderProps(config);
        }
    }
}