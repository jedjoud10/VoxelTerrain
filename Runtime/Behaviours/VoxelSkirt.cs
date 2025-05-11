using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Profiling;
using jedjoud.VoxelTerrain.Octree;
using Unity.Collections.LowLevel.Unsafe;
using jedjoud.VoxelTerrain.Unsafe;
using UnityEditor;

namespace jedjoud.VoxelTerrain.Meshing {
    public class VoxelSkirt : MonoBehaviour {
        public Vector3[] debugSkirtVertices = null;
        public int[] debugSkirtQuads = null;
        public int[] debugSkirtIndices = null;
        public VoxelChunk source;

        public void Complete(NativeArray<float3> vertices, NativeArray<int> quads, NativeArray<int> indices, int vertexCount, int quadCount) {
            MeshFilter filter = GetComponent<MeshFilter>();
            Mesh mesh = new Mesh();
            mesh.vertices = vertices.Reinterpret<Vector3>().GetSubArray(0, vertexCount).ToArray();
            mesh.triangles = quads.GetSubArray(0, quadCount * 6).ToArray();
            debugSkirtVertices = mesh.vertices;
            debugSkirtQuads = mesh.triangles;
            filter.mesh = mesh;
            
            debugSkirtIndices = indices.ToArray();
            debugSkirtQuads = quads.GetSubArray(0, quadCount * 6).ToArray();
            debugSkirtVertices = vertices.Reinterpret<Vector3>().GetSubArray(0, vertexCount).ToArray();

        }

        private void OnDrawGizmosSelected() {
            if (Selection.activeGameObject != gameObject)
                return;

            float s = source.node.size / 64f;
            Vector3 Fetch(int index) {
                Vector3 v = debugSkirtVertices[index];
                return v * s + (Vector3)source.node.position;
            }

            /*
            if (debugSkirtVertices != null) {
                foreach (var v in debugSkirtVertices) {
                    Gizmos.DrawSphere(v * s + (Vector3)source.node.position, 0.3f);
                }
            }
            */

            if (debugSkirtIndices != null) {
                foreach (var i in debugSkirtIndices) {
                    if (i != int.MaxValue) {
                        if (i >= debugSkirtVertices.Length || i < 0) {
                            Debug.LogWarning(i);
                            continue;
                        }

                        Gizmos.DrawSphere(Fetch(i), 0.3f);
                    }
                }
            }

            if (debugSkirtQuads != null) {
                for (var i = 0; i < debugSkirtQuads.Length - 3; i += 3) {
                    int a, b, c;
                    a = debugSkirtQuads[i];
                    b = debugSkirtQuads[i + 1];
                    c = debugSkirtQuads[i + 2];

                    if (a < 0 || b < 0 || c < 0) {
                        continue;
                    }

                    if (a == int.MaxValue || b == int.MaxValue || c == int.MaxValue) {
                        continue;
                    }

                    //Debug.Log($"{a}, {b}, {c}");

                    Gizmos.DrawLine(Fetch(a), Fetch(b));
                    Gizmos.DrawLine(Fetch(b), Fetch(c));
                    Gizmos.DrawLine(Fetch(c), Fetch(a));
                }
            }
        }
    }
}