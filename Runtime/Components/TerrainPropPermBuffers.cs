
using jedjoud.VoxelTerrain.Props;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Segments {
    public class TerrainPropPermBuffers : IComponentData {
        // contains the prop data for each type stored sequentially, but with gaps, so not contiguously
        public ComputeBuffer permBuffer;

        // contains offsets for each prop type inside permBuffer
        public int[] permBufferOffsets;
        public ComputeBuffer permBufferOffsetsBuffer;

        // contains counts for each prop type inside permBuffer
        public int[] permBufferCounts;
        public ComputeBuffer permBufferCountsBuffer;

        // max count for the whole world. can't have more props that this
        public int maxCombinedPermProps;

        // bitset that tells us what elements in permBuffer are in use or are free
        // when we run the compute copy to copy temp to perm buffers we use this beforehand
        // to check for "free" blocks of contiguous memory in the appropriate prop type's region
        public NativeBitArray permPropsInUseBitset;
        public int paddingBitsetBitsCount;
        public int bitsetBlocks;
        public ComputeBuffer permPropsInUseBitsetBuffer;

        // buffer that is CONTINUOUSLY updated right before we submit a compute dispatch call
        // tells us the dst offset in the permBuffer to write our prop data
        public ComputeBuffer permBufferDstCopyOffsetsBuffer;

        // contains the generated matrices for each prop during the copy compute
        // no need to generate matrices in the vertex shader every frame lul
        public ComputeBuffer permMatricesBuffer;

        public ComputeBuffer copyOffsetsBuffer;
        public ComputeBuffer copyTypeLookupBuffer;
        public GraphicsFence? copyFence;

        public struct DebugCounts {
            public int maxPerm;
            public int permOffset;
            public int maxTemp;
            public int tempOffset;
            public int currentInUse;
            public int visibleInstances;
            public int visibleImpostors;
        }

        // x: current perm buffer count
        // y: max perm buffer count
        // z: visible count (SLOW!!!!!!! does a lil readback to the cpu uwu...)
        public DebugCounts[] GetCounts(TerrainPropsConfig config, TerrainPropTempBuffers temp, TerrainPropRenderingBuffers rendering) {
            DebugCounts[] values = new DebugCounts[config.props.Count];

            int[] visibleCountsInterleaved = new int[config.props.Count * 2];
            rendering.visibilityCountersBuffer.GetData(visibleCountsInterleaved);

            for (int i = 0; i < config.props.Count; i++) {
                values[i] = new DebugCounts {
                    maxPerm = permBufferCounts[i],
                    permOffset = permBufferOffsets[i],
                    maxTemp = config.props[i].maxPropsPerSegment,
                    tempOffset = temp.tempBufferOffsets[i],
                    currentInUse = permPropsInUseBitset.CountBits(permBufferOffsets[i], permBufferCounts[i]),
                    visibleInstances = visibleCountsInterleaved[i * 2],
                    visibleImpostors = visibleCountsInterleaved[i * 2 + 1]
                };
            }

            return values;
        }

        public void Init(TerrainPropsConfig config) {
            int types = config.props.Count;
            copyFence = null;

            // Create perm offsets for perm allocation
            maxCombinedPermProps = 0;
            permBufferOffsets = new int[types];
            permBufferCounts = new int[types];
            for (int i = 0; i < types; i++) {
                int count = config.props[i].maxPropsInTotal;
                permBufferOffsets[i] = maxCombinedPermProps;
                permBufferCounts[i] = count;
                maxCombinedPermProps += count;
            }

            // add a few padding bits so that we are always dealing with multiples of 32 bits (size of int)
            bitsetBlocks = (int)math.ceil((float)maxCombinedPermProps / 32.0);
            paddingBitsetBitsCount = bitsetBlocks * 32 - maxCombinedPermProps;
            permPropsInUseBitset = new NativeBitArray(bitsetBlocks * 32, Allocator.Persistent);


            permBuffer = new ComputeBuffer(maxCombinedPermProps, BlittableProp.size, ComputeBufferType.Structured);
            permMatricesBuffer = new ComputeBuffer(maxCombinedPermProps, sizeof(float) * 16, ComputeBufferType.Structured);
            permPropsInUseBitsetBuffer = new ComputeBuffer(bitsetBlocks, sizeof(uint), ComputeBufferType.Structured);
            permBufferOffsetsBuffer = new ComputeBuffer(types, sizeof(uint), ComputeBufferType.Structured);
            permBufferCountsBuffer = new ComputeBuffer(types, sizeof(uint), ComputeBufferType.Structured);
            permBufferDstCopyOffsetsBuffer = new ComputeBuffer(types, sizeof(int), ComputeBufferType.Structured);

            copyOffsetsBuffer = new ComputeBuffer(types, sizeof(int), ComputeBufferType.Structured);
            copyTypeLookupBuffer = new ComputeBuffer(types, sizeof(int), ComputeBufferType.Structured);

            permBufferOffsetsBuffer.SetData(permBufferOffsets);
            permBufferCountsBuffer.SetData(permBufferCounts);
        }
        public void Dispose() {
            permBuffer.Dispose();
            permMatricesBuffer.Dispose();
            permPropsInUseBitset.Dispose();
            permPropsInUseBitsetBuffer.Dispose();
            permBufferOffsetsBuffer.Dispose();
            permBufferCountsBuffer.Dispose();
            permBufferDstCopyOffsetsBuffer.Dispose();
            copyOffsetsBuffer.Dispose();
            copyTypeLookupBuffer.Dispose();
        }
    }
}