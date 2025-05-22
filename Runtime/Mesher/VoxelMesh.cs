using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    // The generated voxel mesh that we can render to the player
    public struct VoxelMesh {
        // AABB that we generated using the vertices
        public Bounds Bounds { get; internal set; }
    }
}