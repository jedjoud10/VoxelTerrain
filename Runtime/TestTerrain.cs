using jedjoud.VoxelTerrain.Props;
using Unity.Mathematics;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Generation.Demo {
    public class TestTerrain : VoxelGraph {
        // Main transform
        public InlineTransform transform1;

        // Noise parameter for the simplex 2D noise
        public Inject<float> scale;
        public Inject<float> amplitude;
        public Inject<float> persistence;
        public Inject<float> lacunarity;
        public Inject<float> cellularScale;
        public Inject<float> cellularOffset;
        public Inject<float> cellularAmplitude;
        public Inject<float> materialHeightNoisy;
        public Inject<float> materialHeight;
        public Inject<float2> smoothing;

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
            Variable<float> fractal = new Fractal<float2>(new Simplex(scale, amplitude), FractalMode.Sum, octaves, lacunarity, persistence).Evaluate(xz);
            Variable<float> cellular = Cellular<float3>.Simple(Sdf.DistanceMetric.Euclidean, 1).Tile(projected.Scaled(cellularScale));

            // Add a floor for the cellular nodes
            Variable<float2> smooth = ((Variable<float2>)smoothing);
            var floored = Sdf.Union(cellular * cellularAmplitude + cellularOffset, y + 10, smooth: smooth.Swizzle<float>("x"));

            // Create a new density parameter
            var density = Sdf.Subtraction(fractal + y, floored, smooth: smooth.Swizzle<float>("y"));
            //var density = cellular;

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
            //output.density = density;
            output.density = y;

            // For now we support only spawning one prop per voxel dispatch, but this will be changed for a more flexible system
            output.prop = GraphUtils.Zero<GpuProp>().With(
                ("position", position),
                ("rotation", rotation),
                ("scale", check.Select<float>(0f, 1f)),
                ("variant", Random.Uniform(position.Scaled(0.2584f), 0.5f).Select<int>(0, 1))
            );

            // Do some funky material picking
            var uhhh = (Noise.Simplex(xz, 0.02f, 1.0f) > 0).Select<int>(1, 0);
            //output.material = 0;
            output.material = ((y + Noise.VoronoiF2(xz, 0.04f, 1.0f) * materialHeightNoisy) > materialHeight).Select<int>(2, uhhh);
        }
    }
}