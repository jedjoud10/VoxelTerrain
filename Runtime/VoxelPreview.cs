using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    [ExecuteInEditMode]
    public class VoxelPreview : MonoBehaviour {
        public ComputeShader surfaceNetsCompute;
        public ComputeShader heightMapCompute;
        private GraphicsBuffer indexBuffer;
        private GraphicsBuffer vertexBuffer;
        private GraphicsBuffer normalsBuffer;
        private GraphicsBuffer colorsBuffer;
        private GraphicsBuffer commandBuffer;
        private GraphicsBuffer atomicCounters;
        private RenderTexture tempVertexTexture;
        private RenderTexture maxHeightAtomic;
        public Material customRenderingMaterial;
        private GraphicsBuffer.IndirectDrawIndexedArgs aaa;
        public bool blocky;
        public bool useHeightSimplification;
        public bool flatshaded;
        public int size = 64;

        public Vector3 scale = Vector3.one;
        public Vector3 offset;

        public void InitializeForSize() {
            if (!isActiveAndEnabled)
                return;

            DisposeBuffers();
            if (indexBuffer != null && indexBuffer.IsValid())
                return;

            indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, size * size * size * 6, sizeof(int));
            vertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, size * size * size, sizeof(float) * 3);
            normalsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, size * size * size, sizeof(float) * 3);
            colorsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, size * size * size, sizeof(float) * 3);
            commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            atomicCounters = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 2, sizeof(uint));

            aaa = new GraphicsBuffer.IndirectDrawIndexedArgs {
                baseVertexIndex = 0,
                instanceCount = 1,
                startIndex = 0,
                startInstance = 0,
                indexCountPerInstance = 0,
            };

            commandBuffer.SetData(new GraphicsBuffer.IndirectDrawIndexedArgs[1] { aaa });

            tempVertexTexture = TextureUtils.Create3DRenderTexture(size, GraphicsFormat.R32_UInt, FilterMode.Point, TextureWrapMode.Repeat, false);
            maxHeightAtomic = TextureUtils.Create2DRenderTexture(size, GraphicsFormat.R32_UInt, FilterMode.Point, TextureWrapMode.Repeat, false);
        }

        private void OnDisable() {
            DisposeBuffers();
        }

        private void OnValidate() {
            GetComponent<VoxelGenerator>().OnPropertiesChanged();
        }


        public void DisposeBuffers() {
            if (indexBuffer != null && indexBuffer.IsValid()) {
                indexBuffer.Dispose();
                vertexBuffer.Dispose();
                normalsBuffer.Dispose();
                commandBuffer.Dispose();
                atomicCounters.Dispose();
                colorsBuffer.Dispose();
                tempVertexTexture.Release();
                maxHeightAtomic.Release();
            }
        }

        public void Meshify(RenderTexture voxels, RenderTexture colors) {
            if (useHeightSimplification) {
                ExecuteHeightMapMesher(voxels, colors, -1, Vector3Int.zero);
            } else {
                ExecuteSurfaceNetsMesher(voxels, colors);
            }

        }

        public void ExecuteSurfaceNetsMesher(RenderTexture voxels, RenderTexture colors) {
            if (atomicCounters == null || !atomicCounters.IsValid())
                return;

            int size = voxels.width;
            atomicCounters.SetData(new uint[2] { 0, 0 });
            commandBuffer.SetData(new GraphicsBuffer.IndirectDrawIndexedArgs[1] { aaa });

            var shader = surfaceNetsCompute;
            shader.SetBool("blocky", blocky);
            shader.SetInt("size", size);

            int minDispatch = Mathf.CeilToInt((float)size / 8.0f);
            int id = shader.FindKernel("CSVertex");
            shader.SetTexture(id, "densities", voxels);
            shader.SetTexture(id, "colorsIn", colors);
            shader.SetBuffer(id, "atomicCounters", atomicCounters);
            shader.SetBuffer(id, "vertices", vertexBuffer);
            shader.SetBuffer(id, "normals", normalsBuffer);
            shader.SetBuffer(id, "colors", colorsBuffer);
            shader.SetBuffer(id, "cmdBuffer", commandBuffer);
            shader.SetTexture(id, "vertexIds", tempVertexTexture);
            shader.Dispatch(id, minDispatch, minDispatch, minDispatch);

            id = shader.FindKernel("CSQuad");
            shader.SetTexture(id, "densities", voxels);
            shader.SetBuffer(id, "indices", indexBuffer);
            shader.SetTexture(id, "vertexIds", tempVertexTexture);
            shader.SetBuffer(id, "cmdBuffer", commandBuffer);
            shader.SetBuffer(id, "atomicCounters", atomicCounters);

            shader.Dispatch(id, minDispatch, minDispatch, minDispatch);
        }

        public void ExecuteHeightMapMesher(RenderTexture voxels, RenderTexture colors, int indexed, Vector3Int chunkOffset) {
            if (atomicCounters == null || !atomicCounters.IsValid())
                return;

            int size = voxels.width;

            if (indexed == -1)
                commandBuffer.SetData(new GraphicsBuffer.IndirectDrawIndexedArgs[1] { aaa });

            var shader = heightMapCompute;
            shader.SetInt("size", size);
            shader.SetVector("vertexOffset", (Vector3)chunkOffset * size);

            Graphics.SetRenderTarget(maxHeightAtomic);
            GL.Clear(false, true, Color.clear);
            Graphics.SetRenderTarget(null);

            int id = shader.FindKernel("CSFlatten");
            shader.SetTexture(id, "densities", voxels);
            shader.SetTexture(id, "maxHeight", maxHeightAtomic);
            shader.Dispatch(id, size / 8, size / 8, size / 8);

            id = shader.FindKernel("CSVertex");
            shader.SetInt("indexOffset", indexed == -1 ? 0 : indexed);
            shader.SetTexture(id, "densities", voxels);
            shader.SetTexture(id, "colorsIn", colors);
            shader.SetTexture(id, "maxHeight", maxHeightAtomic);
            shader.SetBuffer(id, "indices", indexBuffer);
            shader.SetBuffer(id, "vertices", vertexBuffer);
            shader.SetBuffer(id, "normals", normalsBuffer);
            shader.SetBuffer(id, "colors", colorsBuffer);
            shader.SetBuffer(id, "cmdBuffer", commandBuffer);
            shader.Dispatch(id, size / 32, size / 32, 1);
        }

        public void Update() {
            RenderIndexedIndirectMesh();
        }

        public void RenderIndexedIndirectMesh() {
            if (indexBuffer == null || commandBuffer == null || !indexBuffer.IsValid() || !commandBuffer.IsValid())
                return;

            Bounds bounds = new Bounds {
                center = Vector3.zero,
                extents = Vector3.one * 1000.0f,
            };

            var mat = new MaterialPropertyBlock();
            mat.SetBuffer("_Indices", indexBuffer);
            mat.SetBuffer("_Vertices", vertexBuffer);
            mat.SetBuffer("_Normals", normalsBuffer);
            mat.SetBuffer("_Colors", colorsBuffer);
            mat.SetInt("_Flatshaded", (flatshaded || (blocky && !useHeightSimplification)) ? 1 : 0);

            // FIXME: Why do I need to use this instead of just render mesh primitives indexed inderect???
            // Also why do I need to handle the indexing myself???
            Graphics.DrawProceduralIndirect(customRenderingMaterial, bounds, MeshTopology.Triangles, commandBuffer, properties: mat, castShadows: ShadowCastingMode.TwoSided);
        }
    }
}