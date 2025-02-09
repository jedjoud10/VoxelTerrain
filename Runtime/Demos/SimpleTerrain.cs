using Unity.Mathematics;


namespace jedjoud.VoxelTerrain.Generation.Demo {
    public class SimpleTerrain : VoxelGenerator {
        // Main transform
        public InlineTransform transform1;

        // Noise parameter for the simplex 2D noise
        public Inject<float> scale;
        public Inject<float> amplitude;

        public override void Execute(Variable<float3> position, out Variable<float> density, out Variable<float3> color) {
            // Project the position using the main transformation
            var transformer = new ApplyTransformation(transform1);
            var projected = transformer.Transform(position);

            // Split components
            var y = projected.Swizzle<float>("y");
            var xz = projected.Swizzle<float2>("xz");

            // Calculate simple 2D noise
            density = y + Noise.Simplex(xz, scale, amplitude);

            // Simple color based on height uwu
            color = ((y / amplitude) * 0.5f + 0.5f).Swizzle<float3>("xxx");
        }
    }
}