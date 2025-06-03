using UnityEngine;

namespace jedjoud.VoxelTerrain.Props {
    public static class PropQuadGenerator {
        public static Mesh GenerateMuhQuad() {
            Mesh mesh = new Mesh();
            mesh.name = "Procedural Quad";

            // Vertices (quad in XY plane, Z=0)
            Vector3[] vertices = new Vector3[] {
                new Vector3(-1, -1), // Bottom-left
                new Vector3(-1, 1, 0),  // Bottom-right
                new Vector3(1, -1, 0),  // Top-left
                new Vector3(1, 1, 0)    // Top-right
            };

            // Triangles (two triangles to form a quad)
            int[] triangles = new int[] {
                0, 2, 1, // First triangle
                2, 3, 1  // Second triangle
            };

            // UVs
            Vector2[] uvs = new Vector2[] {
                new Vector2(0, 0), // Bottom-left
                new Vector2(1, 0), // Bottom-right
                new Vector2(0, 1), // Top-left
                new Vector2(1, 1)  // Top-right
            };

            // Assign to mesh
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            return mesh;
        }
    }
}