using jedjoud.VoxelTerrain.Generation;
using Unity.Mathematics;

/*
namespace jedjoud.VoxelTerrain.Generation.Demo {
    public class CellularTestTerrain : VoxelGenerator {
        // Main transform
        public InlineTransform transform1;

        // Noise parameter for the ridged 2D noise
        public Inject<float> scale;
        public Inject<float> amplitude;

        // 3D noise parameters
        public Inject<float> scale2;
        public Inject<float> amplitude2;

        // Cellular tiler settings
        public Inject<float> offset;
        public Inject<float> factor;
        public Inject<float> shouldSpawn;
        public Inject<float3> tilerScale;
        public Inject<float3> tilerOffset;
        public float tilingModSize = 16;
        public SdfOps.DistanceMetric distanceFunction;

        public override void Execute(Variable<float3> position, out Variable<float> density, out Variable<float3> color) {
            // Project the position using the main transformation
            var transformer = new ApplyTransformation(transform1);
            var projected = transformer.Transform(position);

            // Split components
            var y = projected.Swizzle<float>("y");
            var xz = projected.Swizzle<float2>("xz");

            // Calculate simple 2D noise
            var evaluated = Noise.Simplex(xz, scale, amplitude).Abs();
            var overlay = Noise.Simplex(projected, scale2, amplitude2);

            // Test
            Cellular<float3>.Distance distanceFunc = (a, b) => SdfOps.Distance(a, b, distanceFunction);
            Cellular<float3>.ShouldSpawn shouldSpawnFunc = (coords) => Hasher.Evaluate<float3, float>(coords) - shouldSpawn;
            var distances = new Cellular<float3>(distanceFunc, shouldSpawnFunc, tilingModSize) { offset = offset, factor = factor }.Tile(projected * tilerScale + tilerOffset);

            // Sum!!!
            density = overlay + y + evaluated + distances;
            color = (evaluated / ((Variable<float>)amplitude / 2.0f)).Swizzle<float3>("xxx") * new float3(0.5f);
        }
    }
}
*/