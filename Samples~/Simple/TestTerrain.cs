using Unity.Mathematics;
using jedjoud.VoxelTerrain.Generation;
using jedjoud.VoxelTerrain.Props;
using Random = jedjoud.VoxelTerrain.Generation.Random;

namespace jedjoud.VoxelTerrain.Demo
{
    public class TestTerrain : ManagedTerrainGraph
    {
        // Main transform
        public InlineTransform transform1;

        // Noise parameter for the simplex 2D noise
        public Inject<float> scale;
        public Inject<float> amplitude;

        // Fractal noise settings 
        public Inject<float2> others;
        public int octaves;

        // Min dot product value to spawn in trees
        public Inject<float> treeMinDotProductVal;

        // Noise + amplitude + offset for prop spawning condition for trees
        public Inject<float> treeSpawnNoiseScale;
        public Inject<float> treeSpawnNoiseAmplitude;
        public Inject<float> treeSpawnNoiseOffset;

        // Scale range for trees
        public Inject<float2> propTreeScale;
        
        // Density spawn range for rocks
        public Inject<float2> rockDensityRangeSpawn;

        // Probability of spawning rock... desu
        public Inject<float> rockSpawnProbability;
        public override void Voxels(VoxelInput input, out VoxelOutput output) {
            // Project the position using the main transformation
            var position = input.position;
            var transformer = new ApplyTransformation(transform1);
            var projected = transformer.Transform(position);

            // Split components
            var y = projected.y;
            var xz = projected.xz;

            // Create simplex and fractal noise
            Simplex<float2> simplex = new Simplex<float2>(scale, amplitude);
            Fractal<float2> fractal = new Fractal<float2>(simplex, FractalMode.Ridged, octaves, others);

            // Execute fractal noise as 2D function
            output = new VoxelOutput(fractal.Evaluate(xz) + y);
        }

        public override void Props(PropInput input, PropContext context) {
            // Check if we have a surface along a specific axis
            PropContext.PossibleSurface surface = context.IsSurfaceAlongAxis(input.position, PropContext.Axis.Y);

            // Check if the surface normal is "up" enough
            Variable<bool> flatSurface = surface.hitNormal.Dot(math.up()) > treeMinDotProductVal;

            // Random rotation with fwd lookup "up" and with random roll
            Variable<float> roll = Random.Range<float3, float>(input.position, 0, 360);
            Variable<quaternion> treeRotation = ((Variable<float3>)math.up()).LookAt(new float3(0, 0, -1), roll);

            // Check if we can spawn trees at this location
            Variable<bool> treeDensityCheck = (Noise.Simplex(input.position, treeSpawnNoiseScale, treeSpawnNoiseAmplitude) + treeSpawnNoiseOffset) > Random.Evaluate<float3, float>(input.position, true);
            Variable<bool> spawnTrees = surface.hit & flatSurface & treeDensityCheck;
            context.SpawnProp(0, spawnTrees, new GenerationProp {
                scale = Random.Range(input.position, propTreeScale),
                position = surface.hitPosition,
                rotation = treeRotation,
                variant = Random.Uniform(input.position, 0.5f).Select<int>(0, 1),
            });

            // Check if we can spawn rocks at this location
            var d = input.density;
            Variable<float2> range = rockDensityRangeSpawn;
            Variable<bool> spawnRocks = d > range.x & d < range.y & Random.Uniform(input.position, rockSpawnProbability);
            context.SpawnProp(1, spawnRocks, new GenerationProp {
                scale = 2f,
                position = input.position,
                rotation = Random.Evaluate<float3, quaternion>(input.position, true).Normalize(),
                variant = Random.Uniform(input.position, 0.5f).Select<int>(0, 1),
            });
        }
    }
}