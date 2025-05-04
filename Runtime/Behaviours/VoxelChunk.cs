using jedjoud.VoxelTerrain.Meshing;
using jedjoud.VoxelTerrain.Octree;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
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
        public BitField32 stitchingMask;
        public float3 other;
        public uint2 relativeOffsetToLod1;
        public VoxelStitch stitch;
        public bool debugValues;

        // ONLY STORED ON THE LOD1 CHUNK.
        public NativeArray<Voxel> blurredPositiveXFacingExtraVoxelsFlat;


        // Check if the chunk has valid voxel data 
        public bool HasVoxelData() {
            return voxels.IsCreated && state == ChunkState.Done || state == ChunkState.Meshing || state == ChunkState.Temp;
        }

        public void OnDrawGizmosSelected() {
            if (Selection.activeGameObject != gameObject)
                return;

            float s = node.size / VoxelUtils.SIZE;

            for (int i = 0; i < StitchUtils.CalculateBoundaryLength(64); i++) {
                uint3 coord = StitchUtils.BoundaryIndexToPos(i, 64);
                int index = VoxelUtils.PosToIndexMorton(coord);
                float d = voxels[index].density;
                if (d > -4 && d < 4) {
                    Gizmos.color = d > 0f ? Color.red : Color.green;
                    Gizmos.DrawSphere((float3)coord * s + node.position, 0.05f);
                }
            }


            /*
            if (debugValues) {
                Gizmos.color = Color.red;
                if (blurredPositiveXFacingExtraVoxelsFlat.IsCreated) {
                    for (int i = 0; i < blurredPositiveXFacingExtraVoxelsFlat.Length; i++) {
                        float2 _pos = (float2)VoxelUtils.IndexToPosMorton2D(i);
                        float3 pos1 = new float3(VoxelUtils.SIZE, _pos);
                        float d1 = blurredPositiveXFacingExtraVoxelsFlat[i].density;

                        if (d1 > -4 && d1 < 4) {
                            Gizmos.color = d1 > 0f ? Color.white : Color.black;
                            Gizmos.DrawSphere(pos1 * s + node.position, 0.05f);
                        }
                    }
                }

                Gizmos.color = Color.white;
                for (int i = 0; i < voxels.Length; i++) {
                    float d = voxels[i].density;
                    float3 p = (float3)VoxelUtils.IndexToPosMorton(i);
                    if (d > -4 && d < 4 && (p.x == 0 || p.x == 1 || p.x == 63)) {
                        Gizmos.color = d > 0f ? Color.red : Color.green;
                        Gizmos.DrawSphere(p * s + node.position, 0.05f);
                    }
                }
            }
            */


            /*

            */

            Gizmos.color = Color.white;
            for (int j = 0; j < 27; j++) {
                uint3 _offset = VoxelUtils.IndexToPos(j, 3);
                int3 offset = (int3)_offset - 1;

                if (neighbourMask.IsSet(j)) {
                    Gizmos.DrawSphere((float3)offset * node.size + node.Center, 5f);
                }
            }

            /*
            Gizmos.color = Color.yellow;
            for (int j = 0; j < 27; j++) {
                uint3 _offset = VoxelUtils.IndexToPos(j, 3);
                int3 offset = (int3)_offset - 1;

                if (stitchingMask.IsSet(j)) {
                    Gizmos.DrawSphere((float3)offset * node.size + node.Center, 5f);
                }
            }
            */

            Gizmos.color = Color.yellow;
            for (int j = 0; j < 27; j++) {
                uint3 _offset = VoxelUtils.IndexToPos(j, 3);
                int3 offset = (int3)_offset - 1;

                if (stitchingMask.IsSet(j)) {
                    Gizmos.DrawSphere((float3)offset * node.size + node.Center, 5f);
                }
            }

            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(node.Center, Vector3.one * node.size);
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

        public void Dispose() {
            voxels.Dispose();

            if (blurredPositiveXFacingExtraVoxelsFlat.IsCreated) {
                blurredPositiveXFacingExtraVoxelsFlat.Dispose();
            }

            stitch?.Dispose();
        }
    }
}