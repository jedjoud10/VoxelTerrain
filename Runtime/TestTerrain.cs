using jedjoud.VoxelTerrain.Props;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;


namespace jedjoud.VoxelTerrain.Generation.Demo {
    public class TestTerrain : VoxelGraph {
        // Main transform
        public InlineTransform transform1;

        // Noise parameter for the simplex 2D noise
        public Inject<float> scale;
        public Inject<float> amplitude;
        public Inject<float> persistence;
        public Inject<float> lacunarity;
        public Inject<float> detailScale;
        public Inject<float> detailProbability;
        public Inject<float> detailAmplitude;
        public Inject<float> materialHeight;
        public Inject<float> materialHeightNoisy;
        public AnimationCurve curve;
        public FractalMode mode;
        [Range(1, 10)]
        public int octaves;


        public override void Execute(AllInputs input, out AllOutputs output) {
            // Project the position using the main transformation
            var position = input.position;
            var transformer = new ApplyTransformation(transform1);
            var projected = transformer.Transform(position);

            // Split components
            var y = projected.Swizzle<float>("y");
            var xz = projected.Swizzle<float2>("xz");

            // Create fractal 2D simplex noise
            Variable<float> fractal = new Fractal<float2>(new Simplex(scale, amplitude), mode, octaves, lacunarity, persistence).Evaluate(xz);

            // Some pyramids...
            Variable<float> extra = Cellular<float2>.Simple(Sdf.DistanceMetric.Chebyshev, detailProbability).Tile(xz.Scaled(detailScale)) * detailAmplitude;
            
            // Create a new density parameter
            var density = (extra + fractal).Curve(curve, -200f, 200f, invert: true) + y;

            // Some checks for prop generation
            Variable<float> test = position.Swizzle<float>("y");
            Variable<float2> flat = position.Swizzle<float2>("xz");
            Variable<bool> check = density > -0.2f & density < 0.2f;
            Variable<float> val = (new Simplex(0.01f, 1.0f).Evaluate(flat) - 0.2f).ClampZeroOne();
            check &= Random.Evaluate<float3, float>(position, false) > 0.95f;

            // Generate a random rotation for the props
            Variable<float3> rotation = Random.Evaluate<float3, float3>(position, true);

            // Set the output values
            output = new AllOutputs();
            output.density = density;

            // For now we support only spawning one prop per voxel dispatch, but this will be changed for a more flexible system
            output.prop = GraphUtils.Zero<GpuProp>().With(
                ("position", position),
                ("rotation", rotation),
                ("scale", check.Select<float>(0f, 1f)),
                ("variant", Random.Uniform(position.Scaled(0.2584f), 0.5f).Select<int>(0, 1))
            );

            // Do some funky material picking
            var uhhh = ((y + Noise.Simplex(position, 0.04f, materialHeightNoisy)) > materialHeight).Select<int>(2, 1);
            output.material = (Noise.Simplex(position, 0.02f, 1.0f) > 0).Select<int>(uhhh, 0);
        }
    }
}