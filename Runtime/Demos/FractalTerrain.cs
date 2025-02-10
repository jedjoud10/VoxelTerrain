using Unity.Mathematics;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Generation.Demo {
    public class FractalTerrain : VoxelGenerator {
        // Main transform
        public InlineTransform transform1;

        // Noise parameter for the simplex 2D noise
        public Inject<float> scale;
        public Inject<float> amplitude;
        public Inject<float> persistence;
        public Inject<float> lacunarity;
        public FractalMode mode;
        public Gradient gradient;
        [Range(1, 10)]
        public int octaves;

        public override void Execute(Variable<float3> position, out Variable<float> density, out Variable<float3> color) {
            // Project the position using the main transformation
            var transformer = new ApplyTransformation(transform1);
            var projected = transformer.Transform(position);

            // Split components
            var y = projected.Swizzle<float>("y");
            var xz = projected.Swizzle<float2>("xz");

            // Create fractal 2D simplex noise
            Fractal<float2> fractal = new Fractal<float2>(new Simplex(scale, amplitude), mode, octaves, lacunarity, persistence);
            //var cached = fractal.Evaluate(xz).Cached(val, "xz");



            density = y + Ramp<float>.Evaluate(fractal.Evaluate(xz), gradient, -(Variable<float>)amplitude, amplitude);



            // Simple color based on height uwu
            color = ((y / amplitude) * 0.5f + 0.5f).Broadcast<float3>();
        }
    }
}