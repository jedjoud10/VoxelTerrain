namespace jedjoud.VoxelTerrain.Generation {
    public class RemapNode<T> : Variable<T> {
        public Variable<T> inputMin;
        public Variable<T> inputMax;
        public Variable<T> outputMin;
        public Variable<T> outputMax;
        public Variable<T> mixer;

        public override void HandleInternal(TreeContext context) {
            mixer.Handle(context);
            inputMin.Handle(context);
            inputMax.Handle(context);
            outputMin.Handle(context);
            outputMax.Handle(context);

            context.DefineAndBindNode<T>(this, $"{context[mixer]}_remapped", $"Remap({context[mixer]}, {context[inputMin]}, {context[inputMax]}, {context[outputMin]}, {context[outputMax]})");
        }
    }

    public class Remapper<T> {
        public Variable<T> inputMin = GraphUtils.Zero<T>();
        public Variable<T> inputMax = GraphUtils.One<T>();
        public Variable<T> outputMin = GraphUtils.Zero<T>();
        public Variable<T> outputMax = GraphUtils.One<T>();

        public Variable<T> Remap(Variable<T> mixer) {
            return new RemapNode<T> {
                mixer = mixer,
                inputMin = inputMin,
                inputMax = inputMax,
                outputMin = outputMin,
                outputMax = outputMax,
            };
        }
    }
}
