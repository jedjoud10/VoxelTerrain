using Unity.Jobs;
using UnityEngine;

// Script added to all game objects that represent a chunk
public partial class VoxelChunk : MonoBehaviour {
    // Either the chunk's own voxel data (in case collisions are enabled) 
    // OR the voxel request data (temp)
    // If null it means the chunk cannot be generated (no voxel data!!)
    public VoxelContainer container;

    // Pending job dependency that we must pass to the mesher
    public JobHandle dependency;

    // Shared generated mesh
    public Mesh sharedMesh;
    public int[] voxelMaterialsLookup;
    public (byte, int)[] triangleOffsetLocalMaterials;

    // Convert a specific sub-mesh index (from physics collision for example) to voxel material index
    public bool TryGetVoxelMaterialFromSubmesh(int submeshIndex, out int voxelMaterialIndex) {
        if (voxelMaterialsLookup != null && submeshIndex < voxelMaterialsLookup.Length) {
            voxelMaterialIndex = voxelMaterialsLookup[submeshIndex];
            return true;
        }

        voxelMaterialIndex = -1;
        return false;
    }

    // Check the global material type of a hit triangle index
    public byte GetTriangleIndexMaterialType(int triangleIndex) {
        if (triangleOffsetLocalMaterials == null) {
            return byte.MaxValue;
        }

        for (int i = triangleOffsetLocalMaterials.Length - 1; i >= 0; i--) {
            (byte localMaterial, int offset) = triangleOffsetLocalMaterials[i];
            if (triangleIndex > offset) {
                return (byte)voxelMaterialsLookup[i];
            }
        }

        return byte.MaxValue;
    }
}