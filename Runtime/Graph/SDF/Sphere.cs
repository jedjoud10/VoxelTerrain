using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Generation {
    public class SdfSphereNode : SdfShapeNode {
        public Variable<float> radius;

        public override void HandleSdfShapeInternal(Variable<float3> projected, TreeContext ctx) {
            radius.Handle(ctx);
            ctx.DefineAndBindNode<float>(this, $"{ctx[projected]}_sphere_sdf", $"sdSphere({ctx[projected]}, {ctx[radius]})");
        }
    }

    public class SdfSphere : SdfShape {
        public Variable<float> radius;
        public SdfSphere(Variable<float> radius, InlineTransform transform = null) : base(transform) {
            this.radius = radius;
        }

        public override Variable<float> Evaluate(Variable<float3> input) {
            return new SdfSphereNode {
                transform = transform,
                radius = radius,
                input = input
            };
        }
    }
}