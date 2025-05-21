using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    // The generated voxel mesh that we can render to the player
    public struct VoxelMesh {
        // Lookup for converting sub-mesh index to voxel material index
        public byte[] VoxelMaterialsLookup { get; internal set; }

        // Lookup for converting triangle indices to material type (given submesh)
        public (byte, int)[] TriangleOffsetLocalMaterials { get; internal set; }

        // Total number of vertices used by this mesh
        public int VertexCount { get; internal set; }

        // Total number of triangles used by this mesh
        public int TriangleCount { get; internal set; }

        // AABB that we generated using the vertices
        public Bounds Bounds { get; internal set; }
    }
}