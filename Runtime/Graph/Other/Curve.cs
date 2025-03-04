using System;
using System.Collections.Generic;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    public class CurveNode : Variable<float> {
        public AnimationCurve curve;
        public Variable<float> mixer;
        public Variable<float> inputMin;
        public Variable<float> inputMax;
        public int size;

        private string curveTextureName;

        public override void Handle(TreeContext context) {
            if (!context.Contains(this)) {
                HandleInternal(context);
            } else {
                context.textures[curveTextureName].readKernels.Add($"CS{context.scopes[context.currentScope].name}");
            }
        }

        public override void HandleInternal(TreeContext context) {
            inputMin.Handle(context);
            inputMax.Handle(context);
            mixer.Handle(context);
            context.Hash(size);

            string textureName = context.GenId($"_curve_texture");
            curveTextureName = textureName;
            context.properties.Add($"Texture2D {textureName}_read;");
            context.properties.Add($"SamplerState sampler{textureName}_read;");

            context.Inject((compute, textures) => {
                Texture2D tex = (Texture2D)textures[textureName].texture;

                float[] points = new float[size];
                for (int i = 0; i < size; i++) {
                    float t = (float)i / size;
                    points[i] = curve.Evaluate(t);
                }
                tex.SetPixelData(points, 0);
                tex.Apply();
            });

            Variable<float> firstRemap = context.AssignTempVariable<float>($"{context[mixer]}_curve_remapped", $"Remap({context[mixer]}, {context[inputMin]}, {context[inputMax]}, 0.0, 1.0)");
            Variable<float> sample = context.AssignTempVariable<float>($"{textureName}_curve", $"{textureName}_read.SampleLevel(sampler{textureName}_read, float2({context[firstRemap]}, 0), 0).x");
            Variable<float> secondRemap = context.AssignTempVariable<float>($"{context[mixer]}_curve_second_remapped", $"Remap({context[sample]}, 0.0, 1.0, {context[inputMin]}, {context[inputMax]})");
            context.DefineAndBindNode<float>(this, $"{textureName}_curve_sampled", context[secondRemap]);

            context.textures.Add(curveTextureName, new CurveTextureDescriptor {
                size = size,
                name = curveTextureName,
                filter = FilterMode.Bilinear,
                wrap = TextureWrapMode.Clamp,
                readKernels = new List<string>() { $"CS{context.scopes[context.currentScope].name}" },
            });
        }
    }
}