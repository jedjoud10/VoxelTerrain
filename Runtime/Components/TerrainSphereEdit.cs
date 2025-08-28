using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using MinMaxAABB = Unity.Mathematics.Geometry.MinMaxAABB;

[assembly: RegisterGenericJobType(typeof(jedjoud.VoxelTerrain.Edits.EditStoreJob<jedjoud.VoxelTerrain.Edits.TerrainSphereEdit>))]
namespace jedjoud.VoxelTerrain.Edits {
    public struct TerrainSphereEdit : IComponentData, IEdit {
        public float3 center;
        public float radius;
        public float4 layers;
        public bool add;

        public MinMaxAABB GetBounds() {
            return MinMaxAABB.CreateFromCenterAndHalfExtents(center, radius);
        }

        public void Modify(float3 position, ref EditVoxel voxel) {
            float sphere = math.length(position - center) - radius;
            
            if (add) {
                voxel.layers = math.select(voxel.layers, layers, sphere < voxel.density); 
                voxel.density = math.min(voxel.density, sphere);
            } else {
                voxel.density = math.max(voxel.density, -sphere);
            }
        }
    }
}