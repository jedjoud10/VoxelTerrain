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
                code = $"({code} - 0.5)";
            }
            
            ctx.DefineAndBindNode<O>(this, "rng", code);
        }
    }

    public class Random {
        public static Variable<O> Evaluate<I, O>(Variable<I> input, bool signed = false) {
            return (new RandomNode<I, O> {
                input = input,
                signed = signed
            });
        }
    }
}