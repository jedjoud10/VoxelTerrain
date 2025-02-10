using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Generation {
    public class SdfBoxNode : SdfShapeNode {
        public Variable<float3> extent;

        public override void HandleSdfShapeInternal(Variable<float3> projected, TreeContext ctx) {
            extent.Handle(ctx);
            ctx.DefineAndBindNode<float>(this, $"{ctx[projected]}_box_sdf", $"sdBox({ctx[projected]}, {ctx[extent]})");
        }
    }

    public class SdfSphereNode : SdfShapeNode {
        public Variable<float> radius;

        public override void HandleSdfShapeInternal(Variable<float3> projected, TreeContext ctx) {
            radius.Handle(ctx);
            ctx.DefineAndBindNode<float>(this, $"{ctx[projected]}_sphere_sdf", $"sdSphere({ctx[projected]}, {ctx[radius]})");
        }
    }

    public class SdfBox : SdfShape {
        public Variable<float3> extent;
        public SdfBox(Variable<float3> extent, InlineTransform transform = null) : base(transform) {
            this.extent = extent;
        }

        public override Variable<float> Evaluate(Variable<float3> input) {
            return new SdfBoxNode {
                transform = transform,
                extent = extent,
                input = input
            };
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