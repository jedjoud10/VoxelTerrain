using System.Collections.Generic;
using UnityEngine;

public class ExecutorTexture {
    public string name;
    public List<string> readKernels;
    public Texture texture;

    public ExecutorTexture(string name, List<string> readKernels, Texture texture) {
        this.name = name;
        this.readKernels = readKernels;
        this.texture = texture;
    }

    public static implicit operator Texture(ExecutorTexture self) {
        return self.texture;
    }

    public virtual void BindToComputeShader(ComputeShader shader) {
        foreach (var readKernel in readKernels) {
            int readKernelId = shader.FindKernel(readKernel);
            shader.SetTexture(readKernelId, name + "_read", texture);
        }
    }

    public virtual void PostDispatchKernel(ComputeShader shader, int kernel) {
    }
}

public class TemporaryExecutorTexture : ExecutorTexture {
    public string writeKernel;
    private int writingKernel;
    public bool mips;

    public TemporaryExecutorTexture(string name, List<string> readKernels, Texture texture, string writeKernel, bool mips) : base(name, readKernels, texture) {
        this.writingKernel = -1;
        this.writeKernel = writeKernel;
        this.mips = mips;
    }

    public override void BindToComputeShader(ComputeShader shader) {
        base.BindToComputeShader(shader);
        int writeKernelId = shader.FindKernel(writeKernel);
        shader.SetTexture(writeKernelId, name + "_write", texture);
        writingKernel = writeKernelId;
    }

    public override void PostDispatchKernel(ComputeShader shader, int kernel) {
        base.PostDispatchKernel(shader, kernel);

        if (writingKernel == kernel && mips && texture is RenderTexture casted) {
            casted.GenerateMips();
        }
    }
}


public class OutputExecutorTexture : ExecutorTexture {
    public OutputExecutorTexture(string name, List<string> readKernels, Texture texture) : base(name, readKernels, texture) {
    }

    public override void BindToComputeShader(ComputeShader shader) {
        foreach (var readKernel in readKernels) {
            int readKernelId = shader.FindKernel(readKernel);
            shader.SetTexture(readKernelId, name + "_write", texture);
        }
    }
}