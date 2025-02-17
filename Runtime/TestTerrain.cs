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
        public FractalMode mode;
        public Gradient gradient;
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


            Variable<float> tahini = Ramp<float>.Evaluate(fractal.Evaluate(xz), gradient, -(Variable<float>)amplitude, amplitude);
            var density = tahini + y;
            //tahini = new SdfBox(new float3(30.0)).Evaluate(position);
            //density = Sdf.Union(y, tahini);



            var color = (Random.Evaluate<float2, float>(xz)).Broadcast<float3>();

            Variable<float> test = position.Swizzle<float>("y");
            Variable<float2> flat = position.Swizzle<float2>("xz");
            Variable<bool> check = density > -0.2f & density < 0.2f;
            Variable<float> val = (new Simplex(0.02f, 1.0f).Evaluate(flat) - 0.2f).ClampZeroOne();
            val -= Random.Evaluate<float3, float>(position);

            output = new AllOutputs();
            output.density = density;
            output.color = color;
            output.prop = (GraphUtils.Zero<Prop>()).With(("position", position), ("scale", check.Select<float>(0.0f, val)));
            //context.SpawnProp(GpuProp.Empty);
            //prop = GpuProp.Empty;
            //prop = prop.With(("xyz", position), ("w", (check & val).Select<float>(0.0f, 1.0f)));
            //prop = prop.With(("x", (Variable<float>)0.0f));
            //prop = position.Swizzle<float4>("xyz", (check & val).Select<float>(0.0f, 1.0f));
        }
    }
}