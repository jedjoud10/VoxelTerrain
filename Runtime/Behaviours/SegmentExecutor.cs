using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    public class SegmentExecutorParameters : ExecutorParameters {
        public int3 position;
        public ComputeBuffer[] tempPropBuffers;
        public ComputeBuffer tempCountersBuffer;
    }

    public class SegmentExecutor : Executor<SegmentExecutorParameters> {
        public SegmentExecutor() : base(SegmentUtils.SEGMENT_SIZE) {
        }

        protected override void CreateMainResources() {
            textures.Add("voxels", new ExecutorTexture {
                name = "voxels",
                readKernels = new List<string>() { "CSProps" },
                texture = TextureUtils.Create3DRenderTexture(size, GraphicsFormat.R32_UInt),
                requestingNodeHash = -1,
                writeKernel = "CSVoxels",
            });
        }

        protected override void ExecuteSetCommands(CommandBuffer commands, ComputeShader shader, SegmentExecutorParameters parameters, int dispatchIndex) {
            LocalKeyword keyword = shader.keywordSpace.FindKeyword(ComputeDispatchUtils.OCTAL_READBACK_KEYWORD);
            commands.DisableKeyword(shader, keyword);

            Vector3 scale = (SegmentUtils.PHYSICAL_SEGMENT_SIZE / VoxelUtils.PHYSICAL_CHUNK_SIZE) * Vector3.one;
            Vector3 offset = (float3)parameters.position * SegmentUtils.PHYSICAL_SEGMENT_SIZE * Vector3.one;
            commands.SetComputeVectorParam(shader, "simple_ffset", offset);
            commands.SetComputeVectorParam(shader, "simple_scale", scale);

            /*
            if (parameters.tempPropBuffers != null && parameters.tempCounters != null) {
                commands.SetComputeVectorParam(shader, "simpleOffset", offset);
                commands.SetComputeVectorParam(shader, "simpleScale", scale);
            }
            */
        }
    }
}
