using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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
                readKernels = new List<string>() { },
                texture = TextureUtils.Create3DRenderTexture(size, GraphicsFormat.R32_UInt),
                writeKernel = "CSVoxels",
                requestingNodeHash = -1,
            });
        }

        protected override void SetComputeParams(CommandBuffer commands, ComputeShader shader, ManagedTerrainSeeder seeder, PreviewExecutorParameters parameters, int kernelIndex) {
            base.SetComputeParams(commands, shader, seeder, parameters, kernelIndex);

            ComputeKeywords.ApplyKeywords(commands, shader, ComputeKeywords.Type.Preview);
            commands.SetComputeVectorParam(shader, "preview_offset", parameters.offset);
            commands.SetComputeVectorParam(shader, "preview_scale", parameters.scale);
        }
    }
}
