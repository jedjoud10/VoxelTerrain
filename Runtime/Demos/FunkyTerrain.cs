using System;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation.Demo {
    public class Test : VoxelGenerator {
        public Inject<float> scale;
        public Inject<float> amplitude;
        public Inject<float> lacunarity;
        public Inject<float> persistence;
        public Inject<float> lacunarity2;
        public Inject<float> persistence2;
        public InlineTransform transform1;
        public InlineTransform transform2;
        public InlineTransform transform3;
        public Gradient gradient;
        public Gradient heightGradient;
        public Gradient spikeGradient2;
        public Inject<float> minRange;
        public Inject<float> maxRange;
        public Inject<float> minRange2;
        public Inject<float> maxRange2;
        public Inject<float> minRange3;
        public Inject<float> maxRange3;
        public Inject<float> scale2;
        public Inject<float> amplitude2;
        public Inject<float> warpScale;
        public Inject<float> warpAmplitude;
        public Inject<float> spikeOffset;
        public FractalMode mode;
        public Texture texture;
        public Inject<float2> textureScale;
        public Inject<float2> textureOffset;
        public Inject<float> smoother;
        public int gradientSize = 128;
        [Range(1, 10)]
        public int octaves;
        [Range(1, 10)]
        public int octaves2;

        public override void Execute(Variable<float3> position, out Variable<float> density, out Variable<float3> color) {
            var transformer = new ApplyTransformation(transform1);
            var pos2 = transformer.Transform(position);
            var output = pos2.Swizzle<float>("y");
            var temp = pos2.Swizzle<float2>("xz");

            // Simple cached 2D base layer
            var voronoi = new Voronoi(scale, amplitude);
            var fractal = Fractal<float2>.Evaluate(temp, voronoi, mode, octaves, lacunarity, persistence);
            var ramp = Ramp<float>.Evaluate(fractal, gradient, minRange, maxRange, gradientSize);

            // 3D secondary transformed layer
            var transformer2 = new ApplyTransformation(transform2);
            var pos3 = transformer2.Transform(pos2);
            var simplex = new Simplex(scale2, amplitude2);

            var warperSimplex = new Simplex(warpScale, warpAmplitude);
            var warped = new Warper<float2>(warperSimplex).Warpinate(pos3.Swizzle<float2>("xz"));

            var fractal2 = Fractal<float2>.Evaluate(warped, simplex, FractalMode.Sum, octaves2, lacunarity2, persistence2);
            var diagonals = Ramp<float>.Evaluate(-((fractal2 - spikeOffset).Min(0.0f)), spikeGradient2, minRange3, maxRange3, gradientSize);
            //var diagonals = new Ramp<float>(spikeGradient2, minRange3, maxRange3).Evaluate((-(fractal2 - spikeOffset).Min(0.0f)));

            density = ramp + output - diagonals;

            var aaaa = new ApplyTransformation(transform3).Transform(pos2);

            var boxed = new SdfBox(new float3(10.0f)).Evaluate(aaaa) + Noise.Simplex(aaaa, 0.4f, 0.2f);
            density = SdfOps.Union(density, boxed);

            var colonThreeFace = new TextureSampler<float2>(texture) { scale = textureScale, offset = textureOffset }.Sample(temp).Swizzle<float>("x") * 10.0f;

            //density -= colonThreeFace; 

            var baseColor = Ramp<float3>.Evaluate(pos2.Swizzle<float>("y"), heightGradient, minRange2, maxRange2, remapOutput: false);
            var otherColor = new float3(0.2);
            color = Variable<float3>.Lerp(baseColor, otherColor, diagonals.Swizzle<float3>("xxx"), true);
            color = Variable<float3>.Lerp(color, float3.zero, colonThreeFace.Swizzle<float3>("xxx"), true);
        }
    }
}