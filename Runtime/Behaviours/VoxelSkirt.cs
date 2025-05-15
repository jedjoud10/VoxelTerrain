using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;

namespace jedjoud.VoxelTerrain.Meshing {
    public class VoxelSkirt : MonoBehaviour {
        public VoxelChunk source;

        public void Complete(NativeArray<float3> vertices, NativeArray<float3> normals, NativeArray<float2> uvs, NativeArray<int> quads, int vertexCount, int triCount) {
            MeshFilter filter = GetComponent<MeshFilter>();
            Mesh mesh = new Mesh();
            mesh.vertices = vertices.Reinterpret<Vector3>().GetSubArray(0, vertexCount).ToArray();
            mesh.triangles = quads.GetSubArray(0, triCount * 3).ToArray();
            mesh.normals = normals.Reinterpret<Vector3>().GetSubArray(0, vertexCount).ToArray();
            mesh.uv = uvs.Reinterpret<Vector2>().GetSubArray(0, vertexCount).ToArray();
            filter.mesh = mesh;
            
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            renderer.enabled = true;
        }

        public void ResetSkirt() {
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            renderer.enabled = false;
        }
    }
}