using jedjoud.VoxelTerrain.Octree;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics.Geometry;

namespace jedjoud.VoxelTerrain {
    public struct TerrainChunk : IComponentData {
        public OctreeNode node;

        // public Mesh sharedMesh;
        // public byte[] voxelMaterialsLookup;
        // public (byte, int)[] triangleOffsetLocalMaterials;
        
        //public MinMaxAABB bounds;
        // public BitField32 neighbours;
    }
}