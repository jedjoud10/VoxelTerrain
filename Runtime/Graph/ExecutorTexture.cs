using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    public class ExecutorTexture {
        public string name;
        public List<string> readKernels;
        public Texture texture;
        public int requestingNodeHash;

        public ExecutorTexture(string name, List<string> readKernels, Texture texture, int requestingNodeHash) {
            this.name = name;
            this.readKernels = readKernels;
            this.texture = texture;
            this.requestingNodeHash = requestingNodeHash;
        }

        public static implicit operator Texture(ExecutorTexture self) {
            return self.texture;
        }

        public virtual void PreDispatch(CommandBuffer commands, ComputeShader shader) {
        }

        public virtual void BindToComputeShader(CommandBuffer commands, ComputeShader shader) {
            foreach (var readKernel in readKernels) {
                int readKernelId = shader.FindKernel(readKernel);
                commands.SetComputeTextureParam(shader, readKernelId, name + "_read", texture);
            }
        }

        public virtual void PostDispatchKernel(CommandBuffer buffer, ComputeShader shader, int kernel) {
        }

        public virtual void Dispose() {
            if (texture is RenderTexture casted) {
                casted.Release();
            }
        }
    }

    public class TemporaryExecutorTexture : ExecutorTexture {
        public string writeKernel;
        private int writingKernel;
        public bool mips;

        public TemporaryExecutorTexture(string name, List<string> readKernels, Texture texture, string writeKernel, bool mips, int requestingNodeHash) : base(name, readKernels, texture, requestingNodeHash) {
            this.writingKernel = -1;
            this.writeKernel = writeKernel;
            this.mips = mips;
        }

        public override void BindToComputeShader(CommandBuffer commands, ComputeShader shader) {
            base.BindToComputeShader(commands, shader);
            int writeKernelId = shader.FindKernel(writeKernel);
            commands.SetComputeTextureParam(shader, writeKernelId, name + "_write", texture);
            writingKernel = writeKernelId;
        }

        public override void PostDispatchKernel(CommandBuffer commands, ComputeShader shader, int kernel) {
            base.PostDispatchKernel(commands, shader, kernel);

            if (writingKernel == kernel && mips && texture is RenderTexture casted) {
                commands.GenerateMips(casted);
            }
        }
    }


    public class OutputExecutorTexture : ExecutorTexture {
        public OutputExecutorTexture(string name, List<string> readKernels, Texture texture, int requestingNodeHash) : base(name, readKernels, texture, requestingNodeHash) {
        }

        public override void BindToComputeShader(CommandBuffer commands, ComputeShader shader) {
            foreach (var readKernel in readKernels) {
                int readKernelId = shader.FindKernel(readKernel);
                commands.SetComputeTextureParam(shader, readKernelId, name + "_write", texture);
            }
        }
    }
}