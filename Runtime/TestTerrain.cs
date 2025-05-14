using Unity.Mathematics;
using UnityEngine;


namespace jedjoud.VoxelTerrain.Generation.Demo {
    public class TestTerrain : VoxelGraph {
        // Main transform
        public InlineTransform transform1;

        // Noise parameter for the simplex 2D noise
        public Inject<float> scale;
        public Inject<float> amplitude;
        public Inject<float> voronoiScale;
        public Inject<float> voronoiAmplitude;
        public Inject<float> warperScale;
        public Inject<float> warperAmplitude;
        public Inject<float2> others;

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
            var first = ((Variable<float2>)others).Swizzle<float>("x");
            var second = ((Variable<float2>)others).Swizzle<float>("y");

            Fractal<float2> fractal = new Fractal<float2>(simplex, FractalMode.Ridged, octaves, first, second);
            var amogus = fractal.Evaluate(xz);

            Warper<float3> warper = new Warper<float3>(new Simplex(warperScale, warperAmplitude));

            var voronoi = new Voronoi(voronoiScale, voronoiAmplitude).Evaluate(warper.Warpinate(projected)); 
            amogus = Sdf.Union(voronoi, amogus);

            output = new AllOutputs();
            output.density = y + amogus;
            output.material = 0;
        }
    }
}