using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

[assembly: RegisterGenericJobType(typeof(jedjoud.VoxelTerrain.Edits.EditStoreJob<jedjoud.VoxelTerrain.Edits.TerrainAddEdit>))]
namespace jedjoud.VoxelTerrain.Edits {
    public struct TerrainAddEdit : IComponentData, IEdit {
        public float3 center;
        public float radius;
        public float strength;
        public float4 layers;
        public bool add;

        public MinMaxAABB GetBounds() {
            return MinMaxAABB.CreateFromCenterAndHalfExtents(center, radius);
        }

        public void Modify(float3 position, ref EditVoxel voxel) {
            float sphere = math.length(position - center);
            float factor = 1 - math.saturate(math.unlerp(0, radius, sphere));
            voxel.density += math.select(1, -1, add) * strength * factor;
            voxel.layers = math.select(voxel.layers, math.lerp(voxel.layers, layers, factor), add);
        }
    }
}