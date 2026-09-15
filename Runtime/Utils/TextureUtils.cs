using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace jedjoud.VoxelTerrain {
    public static class TextureUtils {
        public static RenderTexture Create3DRenderTexture(int size, GraphicsFormat format, FilterMode filter = FilterMode.Trilinear, TextureWrapMode wrap = TextureWrapMode.Clamp, bool mips = false) {
            RenderTexture texture = new RenderTexture(size, size, 0, format);
            texture.width = size;
            texture.height = size;
            texture.depth = 0;
            texture.volumeDepth = size;
            texture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            texture.enableRandomWrite = true;
            texture.useMipMap = mips;
            texture.autoGenerateMips = false;
            texture.filterMode = filter;
            texture.wrapMode = wrap;
            texture.Create();
            return texture;
        }

        public static RenderTexture Create2DRenderTexture(int size, GraphicsFormat format, FilterMode filter = FilterMode.Trilinear, TextureWrapMode wrap = TextureWrapMode.Clamp, bool mips = false) {
            RenderTexture texture = new RenderTexture(size, size, 0, format);
            texture.width = size;
            texture.height = size;
            texture.depth = 0;
            texture.volumeDepth = 1;
            texture.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            texture.enableRandomWrite = true;
            texture.useMipMap = mips;
            texture.autoGenerateMips = false;
            texture.filterMode = filter;
            texture.wrapMode = wrap;
            texture.Create();
            return texture;
        }
    }
}