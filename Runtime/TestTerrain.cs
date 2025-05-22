using Unity.Mathematics;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Generation.Demo {
    public class TestTerrain : ManagedTerrainGraph {
        // Main transform
        public InlineTransform transform1;

        // Noise parameter for the simplex 2D noise
        public Inject<float> scale;
        public Inject<float> amplitude;
        public Inject<float> offset;

        public Inject<float3> detailScale;
        public Inject<float> detailAmplitude;
        public Inject<float> detailProbability;
        public Inject<float> detailOffset;
        public Inject<float> detailSmooth;
        
        public Inject<float> voronoiScale;
        public Inject<float> voronoiAmplitude;
        public Inject<float> warperScale;
        public Inject<float> warperAmplitude;
        public Inject<float2> others;
        public Inject<float3> warpScale;
        public Inject<float3> warpAmplitude;

        [Range(1, 10)]
        public int octaves;

        [Range(1, 10)]
        public int detailOctaves;

        public override void Execute(AllInputs input, out AllOutputs output) {
            // Project the position using the main transformation
            var position = input.position;
            var transformer = new ApplyTransformation(transform1);
            var projected = transformer.Transform(position);

            // Split components
            var y = projected.Swizzle<float>("y");
            var xz = projected.Swizzle<float2>("xz");

            Simplex simplex = new Simplex(scale, amplitude);
            Simplex simplex2 = new Simplex(1f, detailAmplitude);

            var first = ((Variable<float2>)others).Swizzle<float>("x");
            var second = ((Variable<float2>)others).Swizzle<float>("y");

            Fractal<float2> fractal = new Fractal<float2>(simplex, FractalMode.Ridged, octaves, first, second);
            Cellular<float3> detail = Cellular<float3>.Simple(Sdf.DistanceMetric.Chebyshev, detailProbability);
            var amogus = Sdf.Union(fractal.Evaluate(xz) + offset, detail.Tile(detailScale * projected) * detailAmplitude + detailOffset, detailSmooth);

            output = new AllOutputs();
            output.density = y + amogus;
            output.material = 0;
        }
    }
}