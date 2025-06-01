using System;
using jedjoud.VoxelTerrain.Props;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace jedjoud.VoxelTerrain.Segments {
    public class TerrainPropTempBuffers : IComponentData {
        // contains the prop data for each type stored sequentially, but with gaps, so not contiguously
        // allows us to do a SINGLE async readback for ALL the props in a segment
        public ComputeBuffer tempBuffer;

        // temp counter buffer that we write to in the props compute shader
        public ComputeBuffer tempCountersBuffer;

        // contains offsets for each prop type inside tempBuffer
        public int[] tempBufferOffsets;
        public ComputeBuffer tempBufferOffsetsBuffer;

        // max count per segment dispatch (temp)
        public int maxCombinedTempProps;

        // readback buffers
        public NativeArray<int> tempCountersBufferReadback;
        public NativeArray<uint4> tempBufferReadback;

        public void Init(TerrainPropsConfig config) {
            int types = config.props.Count;

            tempBufferOffsets = new int[types];
            maxCombinedTempProps = 0;
            for (int i = 0; i < types; i++) {
                int count = config.props[i].maxPropsPerSegment;
                tempBufferOffsets[i] = maxCombinedTempProps;
                maxCombinedTempProps += count;
            }

            tempBuffer = new ComputeBuffer(maxCombinedTempProps, BlittableProp.size, ComputeBufferType.Structured);
            tempCountersBuffer = new ComputeBuffer(types, sizeof(int), ComputeBufferType.Structured);
            tempBufferOffsetsBuffer = new ComputeBuffer(types, sizeof(int), ComputeBufferType.Structured);

            int[] emptyCounters = new int[types];
            tempCountersBuffer.SetData(emptyCounters);
            tempBufferOffsetsBuffer.SetData(tempBufferOffsets);


            tempCountersBufferReadback = new NativeArray<int>(types, Allocator.Persistent);
            tempBufferReadback = new NativeArray<uint4>(maxCombinedTempProps, Allocator.Persistent);
        }

        public void Dispose() {
            tempBuffer.Dispose();
            tempCountersBuffer.Dispose();
            tempBufferOffsetsBuffer.Dispose();
            tempCountersBufferReadback.Dispose();
            tempBufferReadback.Dispose();
        }
    }
}