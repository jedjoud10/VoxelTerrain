using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    public class CurveNode : Variable<float> {
        public AnimationCurve curve;
        public Variable<float> mixer;
        public Variable<float> inputMin;
        public Variable<float> inputMax;
        public int size;
        public bool invert;

        public GeneratedTextureHelper inner;

        public override void HandleInternal(TreeContext context) {
            inputMin.Handle(context);
            inputMax.Handle(context);
            mixer.Handle(context);
            context.Hash(size);
            context.Hash(invert);

            inner.RegisterFirstTimeIfNeeded(context, (Texture2D tex) => {
                float[] points = new float[size];
                for (int i = 0; i < size; i++) {
                    float t = (float)i / size;
                    points[i] = curve.Evaluate(t);
                }
                tex.SetPixelData(points, 0);
                tex.Apply();
            }, () => {
                return new CurveTextureDescriptor {
                    filter = FilterMode.Bilinear,
                    size = size,
                    wrap = TextureWrapMode.Clamp,
                    requestingNodeHash = this.GetHashCode(),
                };
            });

            inner.RegisterCurrentScopeAsReading(context);

            Variable<float> firstRemap = context.AssignTempVariable<float>($"{context[mixer]}_curve_remapped", $"Remap({context[mixer]}, {context[inputMin]}, {context[inputMax]}, 0.0, 1.0)");

            Variable<float> fetcher = invert ? firstRemap.OneMinus() : firstRemap;
            Variable<float> sampled = inner.SampleLevelAtCoords(context, Variable<float2>.New(fetcher, 0f)).x;
            sampled.Handle(context);
            
            Variable<float> secondRemap = context.AssignTempVariable<float>($"{context[mixer]}_curve_second_remapped", $"Remap(1.0 - {context[sampled]}, 0.0, 1.0, {context[inputMin]}, {context[inputMax]})");
            secondRemap.Handle(context);

            context.DefineAndBindNode<float>(this, $"huh2", $"{context[secondRemap]};");
        }
    }
}