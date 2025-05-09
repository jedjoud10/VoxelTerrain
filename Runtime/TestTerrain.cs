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

            Simplex simplex = new Simplex(scale, amplitude);
            Fractal<float2> fractal = new Fractal<float2>(simplex, FractalMode.Ridged, octaves);

            output = new AllOutputs();
            output.density = y + fractal.Evaluate(xz);
            output.material = 0;
        }
    }
}