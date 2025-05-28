using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    public class ExecutorBuffer {
        public string name;
        public List<string> readKernels;
        public ComputeBuffer buffer;

        public ExecutorBuffer(string name, List<string> readKernels, ComputeBuffer buffer) {
            this.name = name;
            this.readKernels = readKernels;
            this.buffer = buffer;
        }

        public static implicit operator ComputeBuffer(ExecutorBuffer self) {
            return self.buffer;
        }

        public virtual void BindToComputeShader(CommandBuffer commands, ComputeShader shader) {
            foreach (var readKernel in readKernels) {
                int readKernelId = shader.FindKernel(readKernel);
                commands.SetComputeBufferParam(shader, readKernelId, name + "_buffer", buffer);
            }
        }

        public virtual void Dispose() {
            buffer.Dispose();
        }
    }

    /*
    public class ExecutorBufferCounter : ExecutorBuffer {
        int count;

        public ExecutorBufferCounter(string name, List<string> readKernels, int count) : base(name, readKernels, null) {
            this.count = count;
            this.buffer = new ComputeBuffer(count, sizeof(int), ComputeBufferType.Structured);
        }

        public override void BindToComputeShader(CommandBuffer commands, ComputeShader shader) {
            commands.SetBufferData(buffer, new int[count]);
            base.BindToComputeShader(commands, shader);
        }
    }
    */
}