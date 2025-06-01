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
        // Counters for the number of visible (non-culled) props for each prop type
        public ComputeBuffer visiblePropsCountersBuffer;

        // indirection buffer that we use to read data from permBufferMatrices and permBuffer
        // each value represents an index into those buffers
        public ComputeBuffer indirectionBuffer;

        // draw args buffer ykhiibbg
        public GraphicsBuffer drawArgsBuffer;

        public ComputeBuffer maxDistancesBuffer;

        public PropTypeBatchData[] typeBatchData;

        public const int MAX_COMMAND_COUNT_PER_TYPE = 32;
        public const int MAX_INSTANCES_PER_COMMAND = 1024;

        public void Init(int maxCombinedPermProps, TerrainPropsConfig config) {
            int types = config.props.Count;

            indirectionBuffer = new ComputeBuffer(maxCombinedPermProps, sizeof(uint), ComputeBufferType.Structured);

            // do NOT use a struct here / on the GPU!
            // since buffers are aligned to 4 bytes, using a struct on the GPU makes it uh... shit itself... hard
            // just index the raw indices. wtv


            drawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, types * 5 * MAX_COMMAND_COUNT_PER_TYPE, sizeof(uint));
            uint[] args = new uint[types * 5 * MAX_COMMAND_COUNT_PER_TYPE];
            for (int i = 0; i < types; i++) {
                for (int j = 0; j < MAX_COMMAND_COUNT_PER_TYPE; j++) {

                    // Set the IndexCountPerInstance value... (first value inside those 5 grouped ints)
                    args[i * MAX_COMMAND_COUNT_PER_TYPE * 5 + j * 5] = config.props[i].instancedMesh.GetIndexCount(0);
                    args[i * MAX_COMMAND_COUNT_PER_TYPE * 5 + j * 5 + 4] = (uint)(j * MAX_INSTANCES_PER_COMMAND);
                }
            }
            drawArgsBuffer.SetData(args);

            visiblePropsCountersBuffer = new ComputeBuffer(types, sizeof(uint), ComputeBufferType.Structured);
            visiblePropsCountersBuffer.SetData(new uint[types]);

            maxDistancesBuffer = new ComputeBuffer(types, sizeof(float), ComputeBufferType.Structured);
            float[] maxDistances = new float[types];
            for (int i = 0; i < types; i++) {
                maxDistances[i] = config.props[i].instanceMaxDistance;
            }
            maxDistancesBuffer.SetData(maxDistances);

            typeBatchData = new PropTypeBatchData[types];
            for (int i = 0; i < types; i++) {
                typeBatchData[i] = new PropTypeBatchData(
                    config.baked[i].diffuse,
                    config.baked[i].normal,
                    config.baked[i].mask
                );
            }
        }

        public void Dispose() {
            visiblePropsCountersBuffer.Dispose();
            indirectionBuffer.Dispose();
            drawArgsBuffer.Dispose();
            maxDistancesBuffer.Dispose();
        }
    }
}