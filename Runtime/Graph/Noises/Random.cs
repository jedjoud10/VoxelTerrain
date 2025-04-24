namespace jedjoud.VoxelTerrain.Generation {
    public class RandomNode<I, O> : Variable<O> {
        public Variable<I> input;
        public bool signed;

        public override void HandleInternal(TreeContext ctx) {
            input.Handle(ctx);
            int inputDims = VariableType.Dimensionality<I>();
            int outputDims = VariableType.Dimensionality<O>();

            string code = $"hash{outputDims}{inputDims}({ctx[input]})";

            if (signed) {
                code = $"(2.0 * ({code} - 0.5))";
            }

            ctx.DefineAndBindNode<O>(this, "rng", code);
        }
    }

    // TODO: implement random seed system that will keep a local "rng" seed that we can use so that subsequent random() call give different results (without forcing the user to add an offset themselves)
    public class Random {
        public static Variable<O> Evaluate<I, O>(Variable<I> input, bool signed) {
            return (new RandomNode<I, O> {
                input = input,
                signed = signed
            });
        }

        public static Variable<bool> Uniform<I>(Variable<I> input, float probability) {
            return Evaluate<I, float>(input, false) > (1 - probability);
        }
    }
}