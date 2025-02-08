public class RandomNode<I, O> : Variable<O> {
    public Variable<I> input;

    public override void HandleInternal(TreeContext ctx) {
        input.Handle(ctx);
        int inputDims = GraphUtils.Dimensionality<I>();
        int outputDims = GraphUtils.Dimensionality<O>();
        ctx.DefineAndBindNode<O>(this, "rng", $"hash{outputDims}{inputDims}({ctx[input]})");
    }
}

public class Hasher {
    public static Variable<O> Evaluate<I, O>(Variable<I> input) {
        return new RandomNode<I, O> {
            input = input,
        };
    }
}
