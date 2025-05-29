using System.Collections.Generic;
using jedjoud.VoxelTerrain.Segments;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    public class SegmentVoxelExecutorParameters : ExecutorParameters {
        public TerrainSegment segment;
    }

    public class SegmentVoxelExecutor : VolumeExecutor<SegmentVoxelExecutorParameters> {
        public SegmentVoxelExecutor() : base(SegmentUtils.SEGMENT_SIZE) {
        }

        protected override void CreateResources(ManagedTerrainCompiler compiler) {
            base.CreateResources(compiler);
            textures.Add("voxels", new ExecutorTexture {
                name = "voxels",
                readKernels = new List<string>() { },
                texture = TextureUtils.Create3DRenderTexture(size, GraphicsFormat.R32_UInt),
                requestingNodeHash = -1,
                writeKernel = "CSVoxels",
            });
        }

        protected override void SetComputeParams(CommandBuffer commands, ComputeShader shader, ManagedTerrainSeeder seeder, SegmentVoxelExecutorParameters parameters, int kernelIndex) {
            base.SetComputeParams(commands, shader, seeder, parameters, kernelIndex);

            ComputeKeywords.ApplyKeywords(commands, shader, ComputeKeywords.Type.SegmentVoxels);
            commands.SetComputeVectorParam(shader, "segment_offset", (Vector3)parameters.segment.WorldPosition);
            commands.SetComputeVectorParam(shader, "segment_scale", (Vector3)parameters.segment.DispatchScale);
        }
    }
}
