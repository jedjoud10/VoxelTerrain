using System;
using System.Linq;
using jedjoud.VoxelTerrain.Props;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace jedjoud.VoxelTerrain.Segments {
    // not really buffers but yk... gpu shii
    public class TerrainPropRenderingBuffers : IComponentData {
        // counters for the number of visible (non-culled) props for each prop type
        // stores counts for instanced and impostors at the same time, like so:
        // +-----------+-----------+-----------+-----------+
        // |  Type 0   |  Type 0   |  Type 1   |  Type 1   |
        // +-----------+-----------+-----------+-----------+
        // | Instanced | Impostors | Instanced | Impostors |
        // +-----------+-----------+-----------+-----------+
        public ComputeBuffer visibilityCountersBuffer;

        // indirection buffer that we use to read data from permBufferMatrices and permBuffer
        // each value represents an index into those buffers
        public ComputeBuffer instancedIndirectionBuffer;

        // indirection buffer that we use to read data from permBuffer ONLY (we don't need matrices for impostors)
        public ComputeBuffer impostorIndirectionBuffer;

        // draw args buffer for instanced props only
        public GraphicsBuffer instancedDrawArgsBuffer;

        // draw args buffer for impostor props only
        public GraphicsBuffer impostorDrawArgsBuffer;

        // contains bulk texture data (diffuse, normal, mask) stored as texture2Darrays
        public PropTypeInstanceTextureArrays[] typeInstanceTextureArrays;

        // contains bulk texture data (diffuse, normal, mask) stored as texture2Darrays
        public PropTypeImpostorTextureArrays[] typeImpostorsTextureArrays;

        // culling spheres automatically generated for instanced prop types
        public Vector4[] cullingSpheres;

        public void Init(int maxCombinedPermProps, TerrainPropsConfig config) {
            int types = config.props.Count;

            instancedIndirectionBuffer = new ComputeBuffer(maxCombinedPermProps, sizeof(uint), ComputeBufferType.Structured);
            impostorIndirectionBuffer = new ComputeBuffer(maxCombinedPermProps, sizeof(uint), ComputeBufferType.Structured);

            instancedDrawArgsBuffer = CreateIndirectDrawArgBuffer(config, (PropType type) => type.instancedMesh.GetIndexCount(0));
            impostorDrawArgsBuffer = CreateIndirectDrawArgBuffer(config, (PropType type) => 6);

            visibilityCountersBuffer = new ComputeBuffer(types * 2, sizeof(uint), ComputeBufferType.Structured);
            visibilityCountersBuffer.SetData(new uint[types * 2]);

            typeInstanceTextureArrays = new PropTypeInstanceTextureArrays[types];
            for (int i = 0; i < types; i++) {
                if (config.props[i].renderInstances) {
                    typeInstanceTextureArrays[i] = new PropTypeInstanceTextureArrays(config.baked[i]);
                } else {
                    typeInstanceTextureArrays[i] = null;
                }
            }

            typeImpostorsTextureArrays = new PropTypeImpostorTextureArrays[types];
            for (int i = 0; i < types; i++) {
                if (config.props[i].renderInstances && config.props[i].renderImpostors) {
                    typeImpostorsTextureArrays[i] = new PropTypeImpostorTextureArrays(config, config.props[i]);
                } else {
                    typeImpostorsTextureArrays[i] = null;
                }
            }

            cullingSpheres = new Vector4[types];
            for (int i = 0; i < types; i++) {
                if (config.props[i].renderImpostors) {
                    Mesh mesh = config.props[i].instancedMesh;
                    Bounds bounds = mesh.bounds;
                    Vector3 center = bounds.center;
                    float radius = math.cmax(bounds.extents) * 2;
                    cullingSpheres[i] = new Vector4(center.x, center.y, center.z, radius);
                } else {
                    cullingSpheres[i] = Vector4.zero;
                }
            }
        }


        private static GraphicsBuffer CreateIndirectDrawArgBuffer(TerrainPropsConfig config, Func<PropType, uint> indexCountCallback) {
            int types = config.props.Count;

            // do NOT use a struct here / on the GPU!
            // since buffers are aligned to 4 bytes, using a struct on the GPU makes it uh... shit itself... hard
            // just index the raw indices. wtv
            GraphicsBuffer drawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, types * 5, sizeof(uint));
            uint[] args = new uint[types * 5];
            for (int i = 0; i < types; i++) {
                PropType type = config.props[i];

                // Set the IndexCountPerInstance value... (first value inside those 5 grouped ints)
                args[i * 5] = indexCountCallback(type);
            }
            drawArgsBuffer.SetData(args);
            return drawArgsBuffer;
        }

        public void Dispose() {
            visibilityCountersBuffer.Dispose();
            instancedIndirectionBuffer.Dispose();
            instancedDrawArgsBuffer.Dispose();

            impostorIndirectionBuffer.Dispose();
            impostorDrawArgsBuffer.Dispose();
        }
    }
}