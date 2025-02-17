namespace jedjoud.VoxelTerrain.Generation {
    public class RandomNode<I, O> : Variable<O> {
        public Variable<I> input;

        public override void HandleInternal(TreeContext ctx) {
            input.Handle(ctx);
            int inputDims = VariableType.Dimensionality<I>();
            int outputDims = VariableType.Dimensionality<O>();
            ctx.DefineAndBindNode<O>(this, "rng", $"hash{outputDims}{inputDims}({ctx[input]})");
        }
    }

    public class Random {
        public static Variable<O> Evaluate<I, O>(Variable<I> input) {
            return new RandomNode<I, O> {
                input = input,
            };
        }
    }
}