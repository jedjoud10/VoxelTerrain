using jedjoud.VoxelTerrain.Props;
using Unity.Entities;

namespace jedjoud.VoxelTerrain.Segments {
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    public partial class SegmentPropsRenderSystem : SystemBase {

        protected override void OnCreate() {
            RequireForUpdate<TerrainReadySystems>();
            RequireForUpdate<TerrainPropsConfig>();
            RequireForUpdate<TerrainPropStuff>();
        }


        protected override void OnUpdate() {
            var config = SystemAPI.ManagedAPI.GetSingleton<TerrainPropsConfig>();
            var stuff = SystemAPI.ManagedAPI.GetSingleton<TerrainPropStuff>();
            stuff.CullProps(config);

            // TODO: need to figure out how to put this in its own URP pass or at least after all the main opaque objects have rendered
            // it being in the "middle" causes some issues
            stuff.RenderProps(config);
        }
    }
}