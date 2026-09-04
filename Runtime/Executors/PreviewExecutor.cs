using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    public class PreviewExecutorParameters : ExecutorParameters {
        public Vector3 offset;
        public Vector3 scale;
    }

    public class PreviewExecutor : VolumeExecutor<PreviewExecutorParameters> {
        public PreviewExecutor(int size) : base(size) {
        }

        protected override void CreateResources(ManagedTerrainCompiler compiler) {
            base.CreateResources(compiler);
            textures.Add("voxels", new ExecutorTexture {
                name = "voxels",
                texture = TextureUtils.Create3DRenderTexture(size, GpuVoxel.format),
                writeKernels = new List<string>() { "CSVoxels", "CSLayers" },
                requestingNodeHash = -1,
            });
        }

        protected override void SetComputeParams(CommandBuffer commands, ComputeShader shader, PreviewExecutorParameters parameters, int kernelIndex) {
            base.SetComputeParams(commands, shader, parameters, kernelIndex);

            ComputeKeywords.ApplyKeywords(commands, shader, ComputeKeywords.Type.Preview);
            commands.SetComputeVectorParam(shader, "preview_offset", parameters.offset);
            commands.SetComputeVectorParam(shader, "preview_scale", parameters.scale);
        }
    }
}
