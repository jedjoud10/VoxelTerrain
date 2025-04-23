using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    public abstract class TextureDescriptor {
        public FilterMode filter;
        public TextureWrapMode wrap;
        public List<string> readKernels;
        public string name;
        public int requestingNodeHash;

        public abstract ExecutorTexture Create(int size);
    }

    public class TempTextureDescriptor : TextureDescriptor {
        public VariableType type;
        public string writeKernel;
        public bool threeDimensions;
        public int sizeReductionPower;
        public bool mips;

        public override ExecutorTexture Create(int size) {
            RenderTexture rt;
            int textureSize = size / (1 << sizeReductionPower);
            textureSize = Mathf.Max(textureSize, 1);

            if (threeDimensions) {
                rt = TextureUtils.Create3DRenderTexture(textureSize, GraphUtils.ToGfxFormat(type), filter, wrap, mips);
            } else {
                rt = TextureUtils.Create2DRenderTexture(textureSize, GraphUtils.ToGfxFormat(type), filter, wrap, mips);
            }

            return new TemporaryExecutorTexture(name, readKernels, rt, writeKernel, mips, requestingNodeHash);
        }
    }

    public class GradientTextureDescriptor : TextureDescriptor {
        public int size;

        public override ExecutorTexture Create(int volumeSize) {
            Texture2D texture = new Texture2D(size, 1, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
            texture.wrapMode = wrap;
            texture.filterMode = filter;

            return new ExecutorTexture(name, readKernels, texture, requestingNodeHash);
        }
    }

    public class CurveTextureDescriptor : TextureDescriptor {
        public int size;

        public override ExecutorTexture Create(int volumeSize) {
            Texture2D texture = new Texture2D(size, 1, GraphicsFormat.R32_SFloat, TextureCreationFlags.None);
            texture.wrapMode = wrap;
            texture.filterMode = filter;

            return new ExecutorTexture(name, readKernels, texture, requestingNodeHash);
        }
    }

    public class UserTextureDescriptor : TextureDescriptor {
        public Texture texture;

        public override ExecutorTexture Create(int volumeSize) {
            return new ExecutorTexture(name, readKernels, texture, requestingNodeHash);
        }
    }
}