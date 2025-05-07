using System.Collections.Generic;
using jedjoud.VoxelTerrain.Meshing;
using jedjoud.VoxelTerrain.Octree;
using jedjoud.VoxelTerrain.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
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

        public NativeArray<Voxel> voxels;

        public OctreeNode node;
        public ChunkState state;

        // if we skipped meshing the chunk
        public bool skipped;

        [HideInInspector]
        public Mesh sharedMesh;
        [HideInInspector]
        public byte[] voxelMaterialsLookup;
        [HideInInspector]
        public (byte, int)[] triangleOffsetLocalMaterials;
        [HideInInspector]
        public Bounds bounds;
        public BitField32 neighbourMask;
        public BitField32 highLodMask;
        public BitField32 lowLodMask;
        public float3 other;
        public uint2 relativeOffsetToLod1;
        public VoxelStitch stitch;

        public NativeArray<Voxel> negativeBoundaryVoxels;
        public NativeArray<int> negativeBoundaryIndices;
        public NativeArray<float3> negativeBoundaryVertices;
        public NativeCounter negativeBoundaryCounter;
        public JobHandle? copyBoundaryVerticesJobHandle;
        public JobHandle? copyBoundaryVoxelsJobHandle;
        public bool debugValues;

        // Initialize hte chunk with completely new native arrays (during pooled chunk creation)
        public void InitChunk() {
            negativeBoundaryIndices = new NativeArray<int>(StitchUtils.CalculateBoundaryLength(65), Allocator.Persistent);
            negativeBoundaryVertices = new NativeArray<float3>(StitchUtils.CalculateBoundaryLength(65), Allocator.Persistent);
            negativeBoundaryVoxels = new NativeArray<Voxel>(StitchUtils.CalculateBoundaryLength(65), Allocator.Persistent);
            negativeBoundaryCounter = new NativeCounter(Allocator.Persistent);
            copyBoundaryVerticesJobHandle = null;
            copyBoundaryVoxelsJobHandle = null;

            // TODO: Figure out a way to avoid generating voxel containers for chunks that aren't the closest to the player
            // We must keep the chunks loaded in for a bit though, since we need to do some shit with neighbour stitching which requires chunks to have their neighbours voxel data (only at the chunk boundaries though)
            //NativeArray<Voxel> allocated = FetchVoxelsContainer();
            voxels = new NativeArray<Voxel>(65*65*65, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        // Reset the chunk so we can use it to represent a new octree node (don't allocate new native arrays)
        public void ResetChunk(OctreeNode item) {
            node = item;
            voxelMaterialsLookup = null;
            triangleOffsetLocalMaterials = null;
            copyBoundaryVerticesJobHandle = null;
            copyBoundaryVoxelsJobHandle = null;
            negativeBoundaryCounter.Count = 0;
            skipped = false;
            state = ChunkState.Idle;
        }

        // Check if the chunk has valid uniform voxel data
        public bool HasVoxelData() {
            return voxels.IsCreated && (state == ChunkState.Done || state == ChunkState.Meshing || state == ChunkState.Temp);
        }

        // Check if the chunk has valid mesh data at the negative boundary
        public bool HasNegativeBoundaryMeshData() {
            bool job = copyBoundaryVerticesJobHandle.HasValue && copyBoundaryVerticesJobHandle.Value.IsCompleted;
            bool created = negativeBoundaryVertices.IsCreated && negativeBoundaryIndices.IsCreated;
            bool voxels = copyBoundaryVoxelsJobHandle.HasValue && copyBoundaryVoxelsJobHandle.Value.IsCompleted;
            return state == ChunkState.Done && (skipped || (job && voxels && created));
        }

        public List<int> customVertexDebugger = new List<int>();

        public void OnDrawGizmosSelected() {
            if (Selection.activeGameObject != gameObject)
                return;

            float s = node.size / 64f;
            for (int j = 0; j < 27; j++) {
                uint3 _offset = VoxelUtils.IndexToPos(j, 3);
                int3 offset = (int3)_offset - 1;

                if (neighbourMask.IsSet(j)) {
                    Gizmos.color = Color.white;
                    Gizmos.DrawSphere((float3)offset * node.size + node.Center, 5f);
                }

                if (lowLodMask.IsSet(j)) {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawSphere((float3)offset * node.size + node.Center, 5f);
                }

                if (highLodMask.IsSet(j)) {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere((float3)offset * node.size + node.Center, 5f);
                }
            }

            Gizmos.color = Color.white;
            MinMaxAABB bounds = node.Bounds;
            Gizmos.DrawWireCube(bounds.Center, bounds.Extents);

            if (debugValues) {
                /*
                for (int i = 0; i < StitchUtils.CalculateBoundaryLength(64); i++) {
                    int vertexIndex = stitch.boundaryIndices[i];

                    if (vertexIndex != int.MaxValue) {
                        float3 vertex = stitch.boundaryVertices[vertexIndex];
                        Gizmos.DrawSphere(vertex * s + node.position, 0.2f);
                    }
                }
                */

                Vector3 Fetch(int index) {
                    Vector3 v = stitch.vertices[index];
                    return v * s + (Vector3)node.position;
                }

                foreach (var item in customVertexDebugger) {
                    Vector3 v = negativeBoundaryVertices[item];
                    Gizmos.DrawSphere(v * s + (Vector3)node.position, 0.8f);
                }


                if (stitch.stitched) {
                    /*
                    for (int i = 0; i < stitch.vertices.Length; i++) {
                        float3 vertex = stitch.vertices[i];
                        Gizmos.DrawSphere(vertex * s + node.position, 0.2f);
                    }
                    */



                    /*
                    for (int i = 0; i < stitch.debugDataStuff.Length; i++) {
                        float4 vertexAndDebug = stitch.debugDataStuff[i];
                        Gizmos.DrawSphere(vertexAndDebug.xyz * s + node.position, vertexAndDebug.w);
                    }

                    for (var i = 0; i < stitch.triangles.Length - 3; i += 3) {
                        int a, b, c;
                        a = stitch.triangles[i];
                        b = stitch.triangles[i + 1];
                        c = stitch.triangles[i + 2];
                        Gizmos.DrawLine(Fetch(a), Fetch(b));
                        Gizmos.DrawLine(Fetch(b), Fetch(c));
                        Gizmos.DrawLine(Fetch(c), Fetch(a));
                    }
                    */

                    /*
                    for (var i = 0; i < StitchUtils.CalculateBoundaryLength(65); i++) {
                        uint3 _pos = StitchUtils.BoundaryIndexToPos(i, 65);
                        float d1 = stitch.boundaryVoxels[i].density;

                        if (d1 > -4 && d1 < 4) {
                            Gizmos.color = d1 > 0f ? Color.white : Color.black;
                            Gizmos.DrawSphere((float3)_pos * s + node.position, 0.05f);
                        }
                    }
                    */
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

        public void Dispose() {
            voxels.Dispose();
            stitch?.Dispose();

            negativeBoundaryIndices.Dispose();
            negativeBoundaryVertices.Dispose();
            negativeBoundaryCounter.Dispose();
            negativeBoundaryVoxels.Dispose();
        }
    }
}