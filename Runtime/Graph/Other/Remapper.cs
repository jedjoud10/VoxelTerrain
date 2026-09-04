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
}
