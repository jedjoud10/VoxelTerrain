using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    public class SimpleExecutorParameters : ExecutorParameters {
        public Vector3 offset;
        public Vector3 scale;
    }

    public class SimpleExecutor : Executor<SimpleExecutorParameters> {
        public SimpleExecutor(int size) : base(size) {
        }

        protected override void CreateMainResources() {
            textures.Add("voxels", new ExecutorTexture {
                name = "voxels",
                readKernels = new List<string>() { "CSProp" },
                texture = TextureUtils.Create3DRenderTexture(size, GraphicsFormat.R32_UInt),
                writeKernel = "CSVoxel",
                requestingNodeHash = -1,
            });
        }

        protected override void ExecuteSetCommands(CommandBuffer commands, ComputeShader shader, SimpleExecutorParameters parameters, int dispatchIndex) {
            LocalKeyword keyword = shader.keywordSpace.FindKeyword(ComputeDispatchUtils.OCTAL_READBACK_KEYWORD);
            commands.DisableKeyword(shader, keyword);

            commands.SetComputeVectorParam(shader, "simpleOffset", parameters.offset);
            commands.SetComputeVectorParam(shader, "simpleScale", parameters.scale);
        }
    }
}
