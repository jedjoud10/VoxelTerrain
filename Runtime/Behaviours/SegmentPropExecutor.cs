using System.Collections.Generic;
using jedjoud.VoxelTerrain.Segments;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    public class SegmentPropExecutorParameters : ExecutorParameters {
        public TerrainSegment segment;
        public ComputeBuffer tempBufferOffsetsBuffer;
        public ComputeBuffer tempBuffer;
        public ComputeBuffer tempCountersBuffer;
        public Texture segmentVoxelTexture;
    }

    public class SegmentPropExecutor : Executor<SegmentPropExecutorParameters> {
        protected override void SetComputeParams(CommandBuffer commands, ComputeShader shader, ManagedTerrainSeeder seeder, SegmentPropExecutorParameters parameters, int kernelIndex) {
            base.SetComputeParams(commands, shader, seeder, parameters, kernelIndex);

            ComputeKeywords.ApplyKeywords(commands, shader, ComputeKeywords.Type.SegmentProps);
            commands.SetComputeVectorParam(shader, "segment_offset", (Vector3)parameters.segment.WorldPosition);
            commands.SetComputeVectorParam(shader, "segment_scale", (Vector3)parameters.segment.DispatchScale);

            uint[] emptyCounters = new uint[parameters.tempCountersBuffer.count];
            commands.SetBufferData(parameters.tempCountersBuffer, emptyCounters);

            commands.SetComputeBufferParam(shader, kernelIndex, "temp_counters_buffer", parameters.tempCountersBuffer);
            commands.SetComputeBufferParam(shader, kernelIndex, "temp_buffer", parameters.tempBuffer);
            commands.SetComputeBufferParam(shader, kernelIndex, "temp_buffer_offsets_buffer", parameters.tempBufferOffsetsBuffer);

            commands.SetComputeTextureParam(shader, kernelIndex, "voxels_texture_read", parameters.segmentVoxelTexture);
            commands.SetComputeIntParam(shader, "max_combined_temp_props", parameters.tempBuffer.count);

            commands.SetComputeIntParam(shader, "physical_segment_size", SegmentUtils.PHYSICAL_SEGMENT_SIZE);
            commands.SetComputeIntParam(shader, "segment_size", SegmentUtils.SEGMENT_SIZE);
        }
    }
}
