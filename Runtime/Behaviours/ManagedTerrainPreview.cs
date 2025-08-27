using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    [ExecuteInEditMode]
    public class ManagedTerrainPreview : MonoBehaviour {
        public enum PreviewType {
            Mesh,
            Volume,
        }

        public ComputeShader surfaceNetsCompute;
        public ComputeShader unpackPreviewCompute;

        private GraphicsBuffer indexBuffer;
        private GraphicsBuffer vertexBuffer;
        private GraphicsBuffer normalsBuffer;
        private GraphicsBuffer colorsBuffer;
        private GraphicsBuffer commandBuffer;
        private GraphicsBuffer atomicCounters;
        private RenderTexture tempVertexTexture;
        public Material customRenderingMaterial;
        private GraphicsBuffer.IndirectDrawIndexedArgs defaultArgs;
        public bool flatshaded;
        public int size = 64;
        private int initSize = -1;

        public Vector3 scale = Vector3.one;
        public Vector3 offset;

        private PreviewExecutor executor;
        public PreviewType type;

        [HideInInspector]
        public RenderTexture handlesTexture;
        public float volumeValueScale = -0.01f;

        public void InitializeForSize() {
            if (!isActiveAndEnabled)
                return;

            DisposeThings();
            if (indexBuffer != null && indexBuffer.IsValid())
                return;

            initSize = size;
            int volume = initSize * initSize * initSize;
            indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, volume * 6, sizeof(int));
            vertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, volume, sizeof(float) * 3);
            normalsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, volume, sizeof(float) * 3);
            colorsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, volume, sizeof(float) * 3);
            commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            atomicCounters = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 2, sizeof(uint));
            tempVertexTexture = TextureUtils.Create3DRenderTexture(initSize, GraphicsFormat.R32_UInt, FilterMode.Point, TextureWrapMode.Repeat, false);
            handlesTexture = TextureUtils.Create3DRenderTexture(initSize, GraphicsFormat.R32_SFloat, FilterMode.Bilinear, TextureWrapMode.Clamp, false);

            defaultArgs = new GraphicsBuffer.IndirectDrawIndexedArgs {
                baseVertexIndex = 0,
                instanceCount = 1,
                startIndex = 0,
                startInstance = 0,
                indexCountPerInstance = 0,
            };

            commandBuffer.SetData(new GraphicsBuffer.IndirectDrawIndexedArgs[1] { defaultArgs });
        }

        private void OnDisable() {
            DisposeThings();
        }

        private void OnValidate() {
            OnPropertiesChanged();
        }


        public void OnPropertiesChanged() {
            if (!gameObject.activeSelf || !this.isActiveAndEnabled)
                return;

            if (GetComponent<ManagedTerrainGraph>() == null) {
                return;
            }

            if (Application.isPlaying) {
                return;
            }

#if UNITY_EDITOR
            ManagedTerrainCompiler compiler = GetComponent<ManagedTerrainCompiler>();

            if (compiler == null ) {
                Debug.LogWarning($"Compiler is missing or could not be found");
                return;
            }

            if (compiler.ctx == null) {
                compiler.Parse();
            }

            if (compiler.shader == null) {
                return;
            }

            PreviewExecutorParameters parameters = new PreviewExecutorParameters() {
                scale = scale,
                offset = offset,
                kernelName = "CSVoxels",
                updateInjected = true,
                compiler = compiler,
                seed = default,
            };

            if (executor != null)
                executor.DisposeResources();

            executor = new PreviewExecutor(size);
            executor.Execute(parameters);

            parameters.kernelName = "CSLayers";
            executor.Execute(parameters);

            RenderTexture voxels = (RenderTexture)executor.Textures["voxels"];

            switch (type) {
                case PreviewType.Mesh:
                    Meshify(voxels);
                    break;
                case PreviewType.Volume:
                    float tempSize = (size) / 4;
                    int threadGroups = (int)math.ceil(math.max(tempSize, 1));

                    unpackPreviewCompute.SetTexture(0, "srcVoxels", voxels);
                    unpackPreviewCompute.SetTexture(0, "dstValues", handlesTexture);
                    unpackPreviewCompute.SetFloat("volumeValueScale", volumeValueScale);
                    unpackPreviewCompute.Dispatch(0, threadGroups, threadGroups, threadGroups);
                    break;
            }
#endif
        }

        public void DisposeThings() {
            initSize = -1;
            if (indexBuffer != null && indexBuffer.IsValid()) {
                indexBuffer.Dispose();
                vertexBuffer.Dispose();
                normalsBuffer.Dispose();
                commandBuffer.Dispose();
                atomicCounters.Dispose();
                colorsBuffer.Dispose();
                tempVertexTexture.Release();
                handlesTexture.Release();
            }
        }

        public void Meshify(RenderTexture voxels) {
            if (initSize == -1 || voxels.width > initSize) {
                InitializeForSize();
            }

            ExecuteSurfaceNetsMesher(voxels);
        }

        public void ExecuteSurfaceNetsMesher(RenderTexture voxels) {
            if (atomicCounters == null || !atomicCounters.IsValid())
                return;

            int size = voxels.width;
            atomicCounters.SetData(new uint[2] { 0, 0 });
            commandBuffer.SetData(new GraphicsBuffer.IndirectDrawIndexedArgs[1] { defaultArgs });

            var shader = surfaceNetsCompute;
            shader.SetInt("size", size);

            int minDispatchVertex = Mathf.CeilToInt((float)size / 4.0f);
            int id = shader.FindKernel("CSVertex");
            shader.SetTexture(id, "voxels", voxels);
            shader.SetBuffer(id, "atomicCounters", atomicCounters);
            shader.SetBuffer(id, "vertices", vertexBuffer);
            shader.SetBuffer(id, "normals", normalsBuffer);
            shader.SetBuffer(id, "colors", colorsBuffer);
            shader.SetTexture(id, "vertexIds", tempVertexTexture);
            shader.SetBuffer(id, "cmdBuffer", commandBuffer);
            shader.Dispatch(id, minDispatchVertex, minDispatchVertex, minDispatchVertex);

            int minDispatchQuad = Mathf.CeilToInt((float)size / 8.0f);
            id = shader.FindKernel("CSQuad");
            shader.SetTexture(id, "vertexIds", tempVertexTexture);
            shader.SetTexture(id, "voxels", voxels);
            shader.SetBuffer(id, "indices", indexBuffer);
            shader.SetBuffer(id, "cmdBuffer", commandBuffer);
            shader.SetBuffer(id, "atomicCounters", atomicCounters);
            shader.Dispatch(id, minDispatchQuad, minDispatchQuad, minDispatchQuad);
        }

        public void Update() {
            if (GetComponent<ManagedTerrainGraph>() != null && type == PreviewType.Mesh) {
                RenderIndexedIndirectMesh();
            }
        }

        public void RenderIndexedIndirectMesh() {
            if (indexBuffer == null || commandBuffer == null || !indexBuffer.IsValid() || !commandBuffer.IsValid())
                return;

            Bounds bounds = new Bounds {
                center = Vector3.zero,
                extents = Vector3.one * size,
            };

            var mat = new MaterialPropertyBlock();
            mat.SetBuffer("_Indices", indexBuffer);
            mat.SetBuffer("_Vertices", vertexBuffer);
            mat.SetBuffer("_Normals", normalsBuffer);
            mat.SetBuffer("_Colors", colorsBuffer);
            mat.SetInt("_Flatshaded", flatshaded ? 1 : 0);

            // FIXME: Why do I need to use this instead of just render mesh primitives indexed inderect???
            // Also why do I need to handle the indexing myself???
            RenderParams rparams = new RenderParams();
            rparams.matProps = mat;
            rparams.material = customRenderingMaterial;

            // works
            Graphics.DrawProceduralIndirect(customRenderingMaterial, bounds, MeshTopology.Triangles, commandBuffer, properties: mat, castShadows: ShadowCastingMode.TwoSided);

            // does not work. unity pls fix....
            //Graphics.RenderPrimitivesIndexedIndirect(rparams, MeshTopology.Triangles, indexBuffer, commandBuffer);
        }
    }
}