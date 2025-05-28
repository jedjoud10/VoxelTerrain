using Unity.Mathematics;


namespace jedjoud.VoxelTerrain.Generation.Demo {
    public class TestTerrain : ManagedTerrainGraph {
        // Main transform
        public InlineTransform transform1;

        // Offset for the flat plane
        public Inject<float> flatPlaneHeight = 10f;
        public Inject<float> flatPlaneUnionSmooth = 10f;

        // Noise parameter for the simplex 2D noise
        public Inject<float> scale;
        public Inject<float> amplitude;

        // Noise parameters for the 3D voronoi noise
        public Inject<float3> voronoiScale;
        public Inject<float> voronoiAmplitude;

        // Noise parameters for the 2D voronoise noise
        public Inject<float> voronoiseScale;
        public Inject<float> voronoiseAmplitude;

        // Noise parameters for simple 3D simplex detail noise
        public Inject<float3> detailScale;
        public Inject<float> detailAmplitude;

        // Fractal noise settings 
        public Inject<float2> others;
        public int octaves;

        public override void Voxels(VoxelInput input, out VoxelOutput output) {
            // Project the position using the main transformation
            var position = input.position;
            var transformer = new ApplyTransformation(transform1);
            var projected = transformer.Transform(position);

            // Split components
            var y = projected.Swizzle<float>("y");
            var xz = projected.Swizzle<float2>("xz");

            // Create simplex and fractal noise
            Simplex<float2> simplex = new Simplex<float2>(scale, amplitude);
            Fractal<float2> fractal = new Fractal<float2>(simplex, FractalMode.Ridged, octaves, others);

            // Execute fractal noise as 2D function
            Variable<float> density = fractal.Evaluate(xz) + y;

            // Create some extra "detail" voronoi noise
            Voronoi<float3> voronoi = new Voronoi<float3>(voronoiScale, voronoiAmplitude);
            density += voronoi.Evaluate(projected);

            // Add some extra fractal voronoise because why not 
            Voronoise voronoise = new Voronoise(voronoiseScale, voronoiseAmplitude);
            density -= voronoise.Evaluate(xz);

            // Create a flat plane that we will smoothly blend with the rest of the terrain
            Variable<float> flatPlane = y - flatPlaneHeight;
            density = Sdf.Union(density, flatPlane, flatPlaneUnionSmooth);

            // Add some noise details (fractal!!!)
            density += new Fractal<float3>(new Simplex<float3>(detailScale, detailAmplitude), FractalMode.Sum, 6, 1.5f, 0.3f).Evaluate(projected);
            output = new VoxelOutput(density);
        }

        public override void Props(PropInput input, PropContext context) {
            Variable<bool> shouldSpawnProp = input.density > -1 & input.density < 1;
            context.TrySpawnProp(shouldSpawnProp, default);
        }
    }
}