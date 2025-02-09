using Unity.Mathematics;


namespace jedjoud.VoxelTerrain.Generation.Demo {
    public class SimpleWarpingTerrain : VoxelGenerator {
        // Main transform
        public InlineTransform transform1;

        // Noise parameter for the simplex 3D noise
        public Inject<float> scale;
        public Inject<float> amplitude;

        // Noise parameter for the simplex 3D noise used for warping
        public Inject<float> warpingScale;
        public Inject<float> warpingAmplitude;

        // Warping parameters
        public Inject<float3> axialAmplitude;
        public Inject<float3> axialScale;

        public override void Execute(Variable<float3> position, out Variable<float> density, out Variable<float3> color) {
            // Project the position using the main transformation
            var transformer = new ApplyTransformation(transform1);
            var projected = transformer.Transform(position);

            // Split components
            var y = projected.Swizzle<float>("y");

            // Calculate simple 3D noise for warping
            Simplex innerWarpingNoise = new Simplex(warpingScale, warpingAmplitude);

            // Create a warper with a custom function to calculate warping amplitude for each axis given an input
            Warper<float3> warper = new Warper<float3>((Variable<float3> pos) => {
                return innerWarpingNoise.Evaluate(pos).Abs();
            }, axialScale, axialAmplitude);

            // Warp the input point
            var warped = warper.Warpinate(projected);

            // Sample the 3D noise at the warped coord
            density = y + Noise.Simplex(warped, scale, amplitude);

            // Simple color based on height uwu
            color = (((y / amplitude) * 0.5f + 0.5f) * MathOps.Length(((warped - projected) / ((Variable<float>)warpingAmplitude).Swizzle<float3>("xxx")))).Swizzle<float3>("xxx");
            //color = (warped - projected) / ((Variable<float>)warpingAmplitude).Swizzle<float3>("xxx");
        }
    }
}