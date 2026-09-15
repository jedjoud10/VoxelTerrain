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

        public abstract ExecutorTexture Create();
    }

    public class GradientTextureDescriptor : TextureDescriptor {
        public int size;

        public override ExecutorTexture Create() {
            Texture2D texture = new Texture2D(size, 1, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
            texture.wrapMode = wrap;
            texture.filterMode = filter;

            return new ExecutorTexture() {
                name = name,
                readKernels = readKernels,
                texture = texture,
                writeKernels = null,
                requestingNodeHash = requestingNodeHash,
            };
        }
    }

    public class CurveTextureDescriptor : TextureDescriptor {
        public int size;

        public override ExecutorTexture Create() {
            Texture2D texture = new Texture2D(size, 1, GraphicsFormat.R32_SFloat, TextureCreationFlags.None);
            texture.wrapMode = wrap;
            texture.filterMode = filter;

            return new ExecutorTexture() {
                name = name,
                readKernels = readKernels,
                texture = texture,
                writeKernels = null,
                requestingNodeHash = requestingNodeHash,
            };
        }
    }

    public class UserTextureDescriptor : TextureDescriptor {
        public Texture texture;

        public override ExecutorTexture Create() {
            return new ExecutorTexture() {
                name = name,
                readKernels = readKernels,
                texture = texture,
                writeKernels = null,
                requestingNodeHash = requestingNodeHash,
            };
        }
    }
}