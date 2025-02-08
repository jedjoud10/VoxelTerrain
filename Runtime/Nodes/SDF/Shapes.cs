using Unity.Mathematics;

public abstract class SdfShapeNode : Variable<float> {
    public Variable<float3> input;
    public InlineTransform transform;

    public abstract void HandleSdfShapeInternal(Variable<float3> projected, TreeContext ctx);

    public override void HandleInternal(TreeContext ctx) {
        input.Handle(ctx);

        if (transform != null) {
            HandleSdfShapeInternal(transform.ProjectAndBindContext(input, ctx), ctx);
        } else {
            HandleSdfShapeInternal(input, ctx);
        }
    }
}

public abstract class SdfShape {
    public InlineTransform transform;

    protected SdfShape(InlineTransform transform=null) {
        this.transform = transform;
    }

    public abstract Variable<float> Evaluate(Variable<float3> input);
}
