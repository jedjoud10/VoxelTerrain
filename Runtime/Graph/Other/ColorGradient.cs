using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    public class ColorGradientNode : Variable<float4> {
        public Gradient gradient;
        public Variable<float> mixer;
        public int size;

        public GeneratedTextureHelper inner;

        public override void HandleInternal(TreeContext context) {
            mixer.Handle(context);
            context.Hash(size);

            inner.RegisterFirstTimeIfNeeded(context, (Texture2D tex) => {
                Color32[] colors = new Color32[size];
                for (int i = 0; i < size; i++) {
                    float t = (float)i / size;
                    colors[i] = gradient.Evaluate(t);
                }
                tex.SetPixelData(colors, 0);
                tex.Apply();
            }, () => {
                return new GradientTextureDescriptor {
                    filter = FilterMode.Bilinear,
                    size = size,
                    wrap = TextureWrapMode.Clamp,
                    requestingNodeHash = this.GetHashCode(),
                };
            });

            inner.RegisterCurrentScopeAsReading(context);

            Variable<float> firstRemap = context.AssignTempVariable<float>($"{context[mixer]}_gradient_remapped", $"{context[mixer]}");
            Variable<float4> sampled = inner.SampleLevelAtCoords(context, Variable<float2>.New(firstRemap, 0f));
            sampled.Handle(context);

            context.DefineAndBindNode<float4>(this, $"gradient_sampled", $"{context[sampled]};");
        }
    }
}