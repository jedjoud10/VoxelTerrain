using jedjoud.VoxelTerrain.Props;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

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

        // bitset that tells us the props that we are currently allowed to spawn for the segment we are currently executing the compute shaders for
        // gets modified when the prop gets destroyed by the user so that we don't keep spawning it back into the world
        public NativeArray<uint> tempRemovedBitsetEmptyDefault;
        public ComputeBuffer tempRemovedBitsetBuffer;
        public int removedBitsetUintCount;

        public void Init(TerrainPropsConfig config) {
            int types = config.props.Count;

            tempBufferOffsets = new int[types];
            maxCombinedTempProps = 0;
            for (int i = 0; i < types; i++) {
                int count = config.props[i].maxPropsPerSegment;

                // 24 bit limit due to the 3 id bytes in the prop
                if (count >= 16777216) {
                    Debug.LogWarning("Prop temp count is set higher than 16m. Will shit itself if you are expecting to delete the prop entities at runtime.");
                }

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

            removedBitsetUintCount = (int)math.ceil((float)maxCombinedTempProps / 32.0f);
            tempRemovedBitsetBuffer = new ComputeBuffer(removedBitsetUintCount, sizeof(uint), ComputeBufferType.Structured);
            tempRemovedBitsetEmptyDefault = new NativeArray<uint>(removedBitsetUintCount, Allocator.Persistent);
        }

        public void Dispose() {
            tempBuffer.Dispose();
            tempCountersBuffer.Dispose();
            tempBufferOffsetsBuffer.Dispose();
            tempCountersBufferReadback.Dispose();
            tempBufferReadback.Dispose();
            tempRemovedBitsetBuffer.Dispose();
            tempRemovedBitsetEmptyDefault.Dispose();
        }
    }
}