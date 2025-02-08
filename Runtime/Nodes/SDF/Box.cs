using Unity.Mathematics;

public class SdfBoxNode : SdfShapeNode {
    public Variable<float3> extent;

    public override void HandleSdfShapeInternal(Variable<float3> projected, TreeContext ctx) {
        extent.Handle(ctx);
        ctx.DefineAndBindNode<float>(this, $"{ctx[projected]}_asdf", $"sdBox({ctx[projected]}, {ctx[extent]})");
    }
}

public class SdfBox : SdfShape {
    public Variable<float3> extent;
    public SdfBox(Variable<float3> extent, InlineTransform transform=null) : base(transform) {
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