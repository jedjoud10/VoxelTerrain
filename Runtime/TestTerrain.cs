using Unity.Mathematics;


namespace jedjoud.VoxelTerrain.Generation.Demo {
    public class TestTerrain : ManagedTerrainGraph {
        // Main transform
        public InlineTransform transform1;

        // Noise parameter for the simplex 2D noise
        public Inject<float> scale;
        public Inject<float> amplitude;

        // Noise parameters for the 3D voronoi noise
        public Inject<float3> voronoiScale;
        public Inject<float> voronoiAmplitude;

        // Noise parameters for the 2D voronoise noise
        public Inject<float> voronoiseScale;
        public Inject<float> voronoiseAmplitude;

        // Fractal noise settings 
        public Inject<float2> others;
        public int octaves;
        
        public override void Execute(AllInputs input, out AllOutputs output) {
            // Project the position using the main transformation
            var position = input.position;
            var transformer = new ApplyTransformation(transform1);
            var projected = transformer.Transform(position);

            // Split components
            var y = projected.Swizzle<float>("y");
            var xz = projected.Swizzle<float2>("xz");

            // Create simplex and fractal noise
            Simplex simplex = new Simplex(scale, amplitude);
            Fractal<float2> fractal = new Fractal<float2>(simplex, FractalMode.Ridged, octaves, others);

            // Execute fractal noise as 2D function
            Variable<float> density = fractal.Evaluate(xz) + y;

            // Create some extra "detail" voronoi noise
            Voronoi voronoi = new Voronoi(voronoiScale, voronoiAmplitude);
            density += voronoi.Evaluate(projected);

            // Add some extra fractal voronoise because why not 
            Voronoise voronoise = new Voronoise(voronoiseScale, voronoiseAmplitude);
            density -= voronoise.Evaluate(xz);

            output = new AllOutputs();
            output.density = density;
            output.material = 0;
        }
    }
}