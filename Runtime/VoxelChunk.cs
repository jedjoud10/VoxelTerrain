using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    // Script added to all game objects that represent a chunk
    public partial class VoxelChunk : MonoBehaviour {
        [HideInInspector]
        public NativeArray<Voxel> voxels;

        // Pending job dependency that we must pass to the mesher
        [HideInInspector]
        public JobHandle dependency;

        // Shared generated mesh
        [HideInInspector]
        public Mesh sharedMesh;
        [HideInInspector]
        public int[] voxelMaterialsLookup;
        [HideInInspector]
        public (byte, int)[] triangleOffsetLocalMaterials;

        // Get the AABB world bounds of this chunk
        public Bounds GetBounds() {
            return new Bounds {
                min = transform.position,
                max = transform.position + Vector3.one * VoxelUtils.Size * VoxelUtils.VoxelSizeFactor,
            };
        }

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
}