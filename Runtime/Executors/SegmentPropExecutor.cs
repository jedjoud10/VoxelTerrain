using jedjoud.VoxelTerrain.Segments;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    public class SegmentPropExecutorParameters : ExecutorParameters {
        public TerrainSegment segment;
        public ComputeBuffer tempBufferOffsetsBuffer;
        public ComputeBuffer tempBuffer;
        public ComputeBuffer tempCountersBuffer;
        public Texture segmentDensityTexture;
        public ComputeBuffer tempRemovedBitsetBuffer;
        public int enabledPropsTypesFlag;
    }

    public class SegmentPropExecutor : Executor<SegmentPropExecutorParameters> {
        protected override void SetComputeParams(CommandBuffer commands, ComputeShader shader, SegmentPropExecutorParameters parameters, int kernelIndex) {
            base.SetComputeParams(commands, shader, parameters, kernelIndex);

            ComputeKeywords.ApplyKeywords(commands, shader, ComputeKeywords.Type.SegmentProps);
            commands.SetComputeVectorParam(shader, "segment_offset", (Vector3)parameters.segment.WorldPosition);
            commands.SetComputeVectorParam(shader, "segment_scale", (Vector3)parameters.segment.DispatchScale);

            uint[] emptyCounters = new uint[parameters.tempCountersBuffer.count];
            commands.SetBufferData(parameters.tempCountersBuffer, emptyCounters);

            commands.SetComputeBufferParam(shader, kernelIndex, "temp_counters_buffer", parameters.tempCountersBuffer);
            commands.SetComputeBufferParam(shader, kernelIndex, "temp_buffer", parameters.tempBuffer);
            commands.SetComputeBufferParam(shader, kernelIndex, "temp_buffer_offsets_buffer", parameters.tempBufferOffsetsBuffer);
            commands.SetComputeBufferParam(shader, kernelIndex, "destroyed_props_bits_buffer", parameters.tempRemovedBitsetBuffer);

            commands.SetComputeTextureParam(shader, kernelIndex, "densities_texture_read", parameters.segmentDensityTexture);
            commands.SetComputeIntParam(shader, "max_combined_temp_props", parameters.tempBuffer.count);
            commands.SetComputeIntParam(shader, "max_total_prop_types", parameters.tempBufferOffsetsBuffer.count);

            commands.SetComputeIntParam(shader, "enabled_props_flags", parameters.enabledPropsTypesFlag);

            commands.SetComputeIntParam(shader, "physical_segment_size", SegmentUtils.PHYSICAL_SEGMENT_SIZE);
            commands.SetComputeIntParam(shader, "segment_size", SegmentUtils.SEGMENT_SIZE);
            commands.SetComputeIntParam(shader, "segment_size_padded", SegmentUtils.SEGMENT_SIZE_PADDED);
        }
    }
}
