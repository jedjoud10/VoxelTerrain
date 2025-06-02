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

        // Prop settings
        public Inject<float> propSpawnProbability;
        public Inject<float> propMinDotProductVal;
        public Inject<float> propNoiseScale;
        public Inject<float> propNoiseAmplitude;
        public Inject<float> propNoiseOffset;
        public Inject<float2> propDensityRangeSpawn;
        public Inject<float2> propRatScale;
        public Inject<float2> propTreeScale;
        public Inject<float> pushSurfaceAmount;

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
            Variable<bool> spawn = (Noise.Simplex(input.position, propNoiseScale, propNoiseAmplitude) + propNoiseOffset) > Random.Evaluate<float3, float>(input.position, true);

            PropContext.PossibleSurface surface = context.IsSurfaceAlongAxis(input.position, PropContext.Axis.Y);
            Variable<bool> flatSurface = surface.hitNormal.Dot(math.up()) > propMinDotProductVal;

            Variable<quaternion> up = quaternion.identity;
            Variable<float> roll = Random.Range<float3, float>(input.position, 0, 360);
            Variable<quaternion> rotation = surface.hitNormal.LookAt(new float3(0, 0, -1), roll);

            context.SpawnProp(surface.hit & flatSurface & spawn, new Props.GenerationProp {
                scale = Random.Range(input.position, propTreeScale),
                position = surface.hitPosition,
                rotation = ((Variable<float3>)math.up()).LookAt(new float3(0, 0, -1), roll),
                variant = Random.Uniform(input.position, 0.5f).Select<int>(0, 1),
                type = 0,
            });

            var d = input.density;
            Variable<float2> range = propDensityRangeSpawn;
            context.SpawnProp(d > range.x & d < range.y & Random.Uniform(input.position, 0.4f), new Props.GenerationProp {
                scale = 2f,
                position = input.position,
                rotation = Random.Evaluate<float3, quaternion>(input.position, true).Normalize(),
                variant = Random.Uniform(input.position, 0.5f).Select<int>(0, 1),
                type = 1,
            });

            Variable<bool> uhhhh = Random.Uniform(surface.hitPosition);

            context.SpawnProp(surface.hit & !spawn & uhhhh, new Props.GenerationProp {
                scale = Random.Range(input.position, propRatScale),
                position = surface.hitPosition,
                rotation = surface.hitNormal.LookAt(math.up(), roll),
                variant = 0,
                type = 2,
            });

            context.SpawnProp(surface.hit & !spawn & !uhhhh, new Props.GenerationProp {
                scale = 1f,
                position = surface.hitPosition + surface.hitNormal.Normalize().Scaled(pushSurfaceAmount),
                rotation = Random.Evaluate<float3, quaternion>(input.position, true).Normalize(),
                variant = 0,
                type = 3,
            });
        }
    }
}