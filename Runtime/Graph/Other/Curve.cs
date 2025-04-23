using System;
using System.Collections.Generic;
using UnityEngine;
using static TreeEditor.TextureAtlas;

namespace jedjoud.VoxelTerrain.Generation {
    public class CurveNode : Variable<float> {
        public AnimationCurve curve;
        public Variable<float> mixer;
        public Variable<float> inputMin;
        public Variable<float> inputMax;
        public int size;
        public bool invert;

        // We need to store a string here so that subsequent calls to the CurveNode across multiple scopes use the same underyling texture
        private string curveTextureName = "";

        public override void HandleInternal(TreeContext context) {
            inputMin.Handle(context);
            inputMax.Handle(context);
            mixer.Handle(context);
            context.Hash(size);
            context.Hash(invert);

            if (curveTextureName != "" && context.dedupe.Contains(curveTextureName)) {
                context.textures[curveTextureName].readKernels.Add($"CS{context.scopes[context.currentScope].name}");
            } else {
                string textureName = context.GenId($"_curve_texture");
                curveTextureName = textureName;
                context.dedupe.Add(textureName);
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

                context.textures.Add(curveTextureName, new CurveTextureDescriptor {
                    size = size,
                    name = curveTextureName,
                    filter = FilterMode.Bilinear,
                    wrap = TextureWrapMode.Clamp,
                    readKernels = new List<string>() { $"CS{context.scopes[context.currentScope].name}" },
                    requestingNodeHash = this.GetHashCode(),
                });
            }

            Variable<float> firstRemap = context.AssignTempVariable<float>($"{context[mixer]}_curve_remapped", $"Remap({context[mixer]}, {context[inputMin]}, {context[inputMax]}, 0.0, 1.0)");

            string fetcher = invert ? $"1 - {context[firstRemap]}" : context[firstRemap];

            Variable<float> sample = context.AssignTempVariable<float>($"{curveTextureName}_curve", $"{curveTextureName}_read.SampleLevel(sampler{curveTextureName}_read, float2({fetcher}, 0), 0).x");

            Variable<float> secondRemap = context.AssignTempVariable<float>($"{context[mixer]}_curve_second_remapped", $"Remap(1.0 - {context[sample]}, 0.0, 1.0, {context[inputMin]}, {context[inputMax]})");
            context.DefineAndBindNode<float>(this, $"{curveTextureName}_curve_sampled", context[secondRemap]);
        }
    }
}