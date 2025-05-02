using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Profiling;
using jedjoud.VoxelTerrain.Octree;

namespace jedjoud.VoxelTerrain.Meshing {
    public class VoxelStitch : MonoBehaviour {
        public Vector3[] vertices;
        public VoxelChunk lod1;

        // morton encoded too
        public VoxelChunk[] lod0Neighbours;

        // counts the number of LOD0 chunks that finished a blurring request into one of the 4 quadrants of LOD1
        // if this is 4, then we can start stitching
        public int neighbourChunkBlurredSections;

        private void OnDrawGizmosSelected() {
            float s = lod1.node.size / VoxelUtils.SIZE;
            Gizmos.color = Color.blue;
            if (vertices != null) {
                foreach (var v in vertices) {
                    Gizmos.DrawSphere(v * s + (Vector3)lod1.node.position, 0.2f);
                }
            }
        }
    }
}