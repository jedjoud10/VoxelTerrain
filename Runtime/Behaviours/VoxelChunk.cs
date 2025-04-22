using jedjoud.VoxelTerrain.Meshing;
using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    // Script added to all game objects that represent a chunk
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

        // Pending job dependency that we must pass to the mesher
        [HideInInspector]
        public JobHandle? dependency;

        // Chunk position relative to the map origin
        public Vector3Int chunkPosition;

        // Current state of the chunk
        public ChunkState state;

        // Shared generated mesh
        [HideInInspector]
        public Mesh sharedMesh;
        [HideInInspector]
        public int[] voxelMaterialsLookup;
        [HideInInspector]
        public (byte, int)[] triangleOffsetLocalMaterials;

        // Check if the chunk has valid voxel data 
        public bool HasVoxelData() {
            return voxels.IsCreated && state == ChunkState.Done || state == ChunkState.Meshing || state == ChunkState.Temp;
        }


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