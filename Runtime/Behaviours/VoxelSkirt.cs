using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;

namespace jedjoud.VoxelTerrain.Meshing {
    public class VoxelSkirt : MonoBehaviour {
        public Vector3[] debugSkirtVertices = null;
        public Vector3[] debugSkirtNormals = null;
        public Vector3[] debugData = null;
        public int[] debugSkirtQuads = null;
        public int[] debugSkirtIndicesGenerated = null;
        public int[] debugSkirtIndicesCopied = null;
        public VoxelChunk source;

        public void Complete(NativeArray<float3> vertices, NativeArray<float3> normals, NativeArray<float2> uvs, NativeArray<int> quads, NativeArray<int> generated, NativeArray<int> copied, int vertexCount, int triCount, NativeList<float3> data) {
            MeshFilter filter = GetComponent<MeshFilter>();
            Mesh mesh = new Mesh();
            mesh.vertices = vertices.Reinterpret<Vector3>().GetSubArray(0, vertexCount).ToArray();
            mesh.triangles = quads.GetSubArray(0, triCount * 3).ToArray();
            mesh.normals = normals.Reinterpret<Vector3>().GetSubArray(0, vertexCount).ToArray();
            mesh.uv = uvs.Reinterpret<Vector2>().GetSubArray(0, vertexCount).ToArray();
            debugSkirtVertices = mesh.vertices;
            debugSkirtNormals = mesh.normals;
            debugSkirtQuads = mesh.triangles;
            filter.mesh = mesh;

            debugSkirtIndicesGenerated = generated.ToArray();
            debugSkirtIndicesCopied = copied.ToArray();
            debugSkirtQuads = quads.GetSubArray(0, triCount * 3).ToArray();
            debugSkirtVertices = vertices.Reinterpret<Vector3>().GetSubArray(0, vertexCount).ToArray();
            debugData = data.AsArray().Reinterpret<Vector3>().ToArray();
            //Debug.Log($"T: {triCount}, V: {vertexCount}");

            MeshRenderer renderer = GetComponent<MeshRenderer>();
            renderer.enabled = true;
        }

        public void ResetSkirt() {
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            renderer.enabled = false;
        }

        public int faceIndex;
        public uint2 debugIndex;
        public bool fetchFromOg;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected() {
            if (Selection.activeGameObject != gameObject || source == null)
                return;

            float s = source.node.size / 64f;
            Vector3 Fetch(int index) {
                Vector3 v = debugSkirtVertices[index];
                return v * s + (Vector3)(float3)source.node.position;
            }

            /*
            if (fetchFromOg) {
                int indexu = VoxelUtils.PosToIndex2D(debugIndex, VoxelUtils.SIZE);
                int temp = debugSkirtIndicesCopied[indexu + faceIndex * VoxelUtils.SIZE * VoxelUtils.SIZE];

                Gizmos.color = Color.green;
                if (temp != int.MaxValue) {
                    Gizmos.DrawSphere(debugSkirtVertices[temp] * s + (Vector3)source.node.position, 0.8f);
                }
            } else {
                int indexu = VoxelUtils.PosToIndex2D(debugIndex, VoxelUtils.SKIRT_SIZE);
                int temp = debugSkirtIndicesGenerated[indexu + faceIndex * VoxelUtils.SKIRT_FACE];

                Gizmos.color = Color.green;
                if (temp != int.MaxValue) {
                    Gizmos.DrawSphere(debugSkirtVertices[temp] * s + (Vector3)source.node.position, 0.8f);
                }
            }
            */

            /*
            Gizmos.color = Color.white;
            foreach (Vector3 b in debugData) {
                Gizmos.DrawWireSphere(b * s + (Vector3)source.node.position, 0.4f);
            }
            */

            Gizmos.color = Color.red;
            if (debugSkirtIndicesCopied != null) {
                foreach (var i in debugSkirtIndicesCopied) {
                    if (i != int.MaxValue && i < debugSkirtVertices.Length) {
                        Gizmos.DrawSphere(Fetch(i), 0.3f);
                    }
                }
            }

            Gizmos.color = Color.green;
            if (debugSkirtIndicesGenerated != null) {
                foreach (var i in debugSkirtIndicesGenerated) {
                    if (i != int.MaxValue && i < debugSkirtVertices.Length) {
                        Gizmos.DrawSphere(Fetch(i), 0.3f);
                        Gizmos.DrawRay(Fetch(i), debugSkirtNormals[i]);
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
#endif
    }
}