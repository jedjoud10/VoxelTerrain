using System.Collections.Generic;
using System;
using UnityEngine;
using Unity.Entities;
using System.Linq;

namespace jedjoud.VoxelTerrain.Props {
    public class PropTypeInstanceTextureArrays {
        public Texture2DArray diffuse;
        public Texture2DArray normal;
        public Texture2DArray mask;

        public PropTypeInstanceTextureArrays(PropType type) {
            Texture2D[] diffuse = type.variants.Select(variant => variant.textures.diffuse).ToArray();
            Texture2D[] normal = type.variants.Select(variant => variant.textures.normal).ToArray();
            Texture2D[] mask = type.variants.Select(variant => variant.textures.mask).ToArray();

            this.diffuse = CreateTexArray(diffuse, false, Texture2D.whiteTexture);
            this.normal = CreateTexArray(normal, true, Texture2D.normalTexture);
            this.mask = CreateTexArray(mask, true, Texture2D.whiteTexture);
        }

        private static Texture2DArray CreateTexArray(Texture2D[] textures, bool linear, Texture2D fallback) {
            if (textures == null || textures.Length == 0 || textures[0] == null)
                return null;

            int width = textures[0].width;
            int height = textures[0].height;
            int mips = textures[0].mipmapCount;
            TextureFormat format = textures[0].format;
            FilterMode filterMode = textures[0].filterMode;

            foreach (Texture2D tex in textures) {
                if (tex == null)
                    throw new Exception("That is why I don't even bother");
                if (tex.width != width || tex.height != height)
                    throw new Exception("All textures must have the same width and height!!!! Desu nee");
                if (tex.format != format)
                    throw new Exception("All textures must have the same format!!!");
                if (tex.mipmapCount != mips)
                    throw new Exception("All textures must have the same number of mipmaps!!!");
                if (tex.filterMode != filterMode)
                    throw new Exception("All textures must have the same filter mode!!!");
            }

            Texture2DArray array = new Texture2DArray(width, height, textures.Length, format, mips, linear);
            array.filterMode = filterMode;

            for (int i = 0; i < textures.Length; i++) {
                Texture2D tex = textures[i];
                for (int m = 0; m < mips; m++) {
                    Graphics.CopyTexture(tex, 0, m, array, i, m);
                }
            }

            return array;
        }
    }
}