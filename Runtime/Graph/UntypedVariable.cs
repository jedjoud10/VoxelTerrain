namespace jedjoud.VoxelTerrain.Generation {
    public abstract class UntypedVariable {
        public virtual void Handle(TreeContext context) {
            if (!context.Contains(this)) {
                HandleInternal(context);
            }
        }
        public abstract void HandleInternal(TreeContext context);
    }
}