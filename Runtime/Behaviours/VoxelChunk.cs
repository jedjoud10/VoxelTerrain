using jedjoud.VoxelTerrain.Octree;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    public partial class VoxelChunk : MonoBehaviour {
        public enum ChunkState {
            // Just spawned in
            Idle,

            // Chunk is in the process of generating voxels
            VoxelGeneration,

            // Chunk is waiting for voxel GPU readback
            // TODO: Remove this when Unity supports Indirect Indexed rendering using Shader Graph
            VoxelReadback,

            // Chunk has voxel data, but no mesh yet. Neighbours can use its data now...
            Temp,

            // Chunk is in the process of meshing
            Meshing,

            // Chunk is done meshing for now
            Done,
        }

        [HideInInspector]
        public NativeArray<Voxel> voxels;

        public OctreeNode node;
        public ChunkState state;

        [HideInInspector]
        public Mesh sharedMesh;
        [HideInInspector]
        public byte[] voxelMaterialsLookup;
        [HideInInspector]
        public (byte, int)[] triangleOffsetLocalMaterials;
        [HideInInspector]
        public Bounds bounds;
        public BitField32 neighbourMask;

        // Check if the chunk has valid voxel data 
        public bool HasVoxelData() {
            return voxels.IsCreated && state == ChunkState.Done || state == ChunkState.Meshing || state == ChunkState.Temp;
        }

        public void OnDrawGizmosSelected() {
            for (int j = 0; j < 27; j++) {
                uint3 _offset = VoxelUtils.IndexToPos(j, 3);
                int3 offset = (int3)_offset - 1;

                if (neighbourMask.IsSet(j)) {
                    Gizmos.DrawSphere((float3)offset * node.size + node.Center, 5f);
                }
            }
        }
            
        // Get the AABB world bounds of this chunk
        public Bounds GetBounds() {
            return bounds;
        }

        // Convert a specific sub-mesh index (from physics collision for example) to voxel material index
        public bool TryLookupMaterialSubmeshIndex(int submeshIndex, out byte material) {
            if (voxelMaterialsLookup != null && submeshIndex < voxelMaterialsLookup.Length) {
                material = voxelMaterialsLookup[submeshIndex];
                return true;
            }

            material = byte.MaxValue;
            return false;
        }

        // Check the global material type of a hit triangle index
        public bool TryLookupMaterialTriangleIndex(int triangleIndex, out byte material) {
            if (triangleOffsetLocalMaterials == null) {
                Debug.LogWarning("Material lookup array is not set...");
                material = byte.MaxValue;
                return false;
            }

            // Goes through each submesh and checks if the triangle index is valid for each one
            // When we find one with a range that encapsulates the triangle index, then we return the material for that submesh (given the index again)
            for (int i = triangleOffsetLocalMaterials.Length - 1; i >= 0; i--) {
                (byte mat, int offset) = triangleOffsetLocalMaterials[i];
                if (triangleIndex >= offset) {
                    material = mat;
                    return true;
                }
            }

            material = byte.MaxValue;
            return false;
        }
    }
}