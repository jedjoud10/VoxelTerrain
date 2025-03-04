using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    public class ColorGradientNode : Variable<float4> {
        public Gradient gradient;
        public Variable<float> mixer;
        public int size;

        private string gradientTextureName;

        public override void Handle(TreeContext context) {
            if (!context.Contains(this)) {
                HandleInternal(context);
            } else {
                context.textures[gradientTextureName].readKernels.Add($"CS{context.scopes[context.currentScope].name}");
            }
        }

        public override void HandleInternal(TreeContext context) {
            mixer.Handle(context);
            context.Hash(size);

            string textureName = context.GenId($"_gradient_texture");
            gradientTextureName = textureName;
            context.properties.Add($"Texture2D {textureName}_read;");
            context.properties.Add($"SamplerState sampler{textureName}_read;");

            context.Inject((compute, textures) => {
                Texture2D tex = (Texture2D)textures[textureName].texture;

                Color32[] colors = new Color32[size];
                for (int i = 0; i < size; i++) {
                    float t = (float)i / size;
                    colors[i] = gradient.Evaluate(t);
                }
                tex.SetPixelData(colors, 0);
                tex.Apply();
            });

            Variable<float> firstRemap = context.AssignTempVariable<float>($"{context[mixer]}_gradient_remapped", $"{context[mixer]}");
            context.DefineAndBindNode<float4>(this, $"{textureName}_gradient_sampled", $"{textureName}_read.SampleLevel(sampler{textureName}_read, float2({context[firstRemap]}, 0), 0)");

            context.textures.Add(gradientTextureName, new GradientTextureDescriptor {
                size = size,
                name = gradientTextureName,
                filter = FilterMode.Bilinear,
                wrap = TextureWrapMode.Clamp,
                readKernels = new List<string>() { $"CS{context.scopes[context.currentScope].name}" },
            });
        }
    }
}