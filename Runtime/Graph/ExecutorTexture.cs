using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    public class ExecutorTexture {
        public string name;
        public string writeKernel;
        public List<string> readKernels;
        public Texture texture;
        public int requestingNodeHash;

        public static implicit operator Texture(ExecutorTexture self) {
            return self.texture;
        }

        public virtual void BindToComputeShader(CommandBuffer commands, ComputeShader shader) {
            if (writeKernel != null && writeKernel != "") {
                int writeKernelId = shader.FindKernel(writeKernel);
                commands.SetComputeTextureParam(shader, writeKernelId, name + "_texture_write", texture);
            }

            foreach (var readKernel in readKernels) {
                int readKernelId = shader.FindKernel(readKernel);
                commands.SetComputeTextureParam(shader, readKernelId, name + "_texture_read", texture);
            }
        }

        public virtual void Dispose() {
            if (texture is RenderTexture casted) {
                casted.Release();
            }
        }
    }

    /*
    public class TemporaryExecutorTexture : ExecutorTexture {

        public bool mips;

        public TemporaryExecutorTexture(string name, List<string> readKernels, Texture texture, string writeKernel, bool mips, int requestingNodeHash) : base(name, readKernels, texture, requestingNodeHash) {
            this.writingKernel = -1;
            this.writeKernel = writeKernel;
            this.mips = mips;
        }

        public override void BindToComputeShader(CommandBuffer commands, ComputeShader shader) {
            base.BindToComputeShader(commands, shader);
            int writeKernelId = shader.FindKernel(writeKernel);
            commands.SetComputeTextureParam(shader, writeKernelId, name + "_tex_w", texture);
            writingKernel = writeKernelId;
        }
    }


    public class OutputExecutorTexture : ExecutorTexture {
        public OutputExecutorTexture(string name, List<string> readKernels, Texture texture, int requestingNodeHash) : base(name, readKernels, texture, requestingNodeHash) {
        }
    }
    */
}