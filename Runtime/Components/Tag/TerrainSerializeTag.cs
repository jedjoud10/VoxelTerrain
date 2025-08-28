using Unity.Entities;

namespace jedjoud.VoxelTerrain.Serialization {
    /// <summary>
    /// Tagged entity to let the terrain know that it should serialize the data
    /// After the terrain is serialized, the TerrainSerializationData singleton will be added to the world
    /// </summary>
    public struct TerrainSerializeTag : IComponentData {
    }
}