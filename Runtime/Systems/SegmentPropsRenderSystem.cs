using jedjoud.VoxelTerrain.Props;
using Unity.Entities;

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