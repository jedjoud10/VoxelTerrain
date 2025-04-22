using jedjoud.VoxelTerrain.Props;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;


namespace jedjoud.VoxelTerrain.Generation.Demo {
    public class TestTerrain : VoxelGraph {
        // Main transform
        public InlineTransform transform1;

        // Noise parameter for the simplex 2D noise
        public Inject<float> scale;
        public Inject<float> amplitude;
        public Inject<float> persistence;
        public Inject<float> lacunarity;
        public Inject<float> detail;
        public FractalMode mode;
        public AnimationCurve curve;
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

            // Create fractal 2D simplex noise
            Fractal<float2> fractal = new Fractal<float2>(new Simplex(scale, amplitude), mode, octaves, lacunarity, persistence);
            //var cached = fractal.Evaluate(xz).Cached(val, "xz");
            Variable<float> tahini = fractal.Evaluate(xz).Curve(curve, -(Variable<float>)amplitude, amplitude);
            Variable<float> extra = Noise.VoronoiF2(position * new float3(1, 3, 1), 0.04f, 4.0f) * detail;
            Variable<float> amogus = tahini + extra;
            var density = amogus + y;
            //tahini = new SdfBox(new float3(30.0)).Evaluate(position);
            //density = Sdf.Union(y, tahini);



            var color = (Random.Evaluate<float2, float>(xz)).Broadcast<float3>();

            Variable<float> test = position.Swizzle<float>("y");
            Variable<float2> flat = position.Swizzle<float2>("xz");
            Variable<bool> check = density > -0.2f & density < 0.2f;
            Variable<float> val = (new Simplex(0.01f, 1.0f).Evaluate(flat) - 0.2f).ClampZeroOne();
            check &= Random.Evaluate<float3, float>(position) > 0.99f;

            Variable<float3> rotation = Random.Evaluate<float3, float3>(position, true);

            output = new AllOutputs();
            output.density = density;
            output.density = y;
            output.color = new float3(1.0);
            output.prop = (GraphUtils.Zero<Prop>()).With(
                ("position", position),
                ("rotation", rotation),
                ("scale", check.Select<float>(0.0f, val * 3))
            );
            output.material = 0;
            //output.material = (Noise.Simplex(position, 0.2f, 1.0f) > 0).Select<int>(1, 0);
            //context.SpawnProp(GpuProp.Empty);
            //prop = GpuProp.Empty;
            //prop = prop.With(("xyz", position), ("w", (check & val).Select<float>(0.0f, 1.0f)));
            //prop = prop.With(("x", (Variable<float>)0.0f));
            //prop = position.Swizzle<float4>("xyz", (check & val).Select<float>(0.0f, 1.0f));
        }
    }
}