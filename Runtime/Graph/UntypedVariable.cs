using System;


namespace jedjoud.VoxelTerrain.Generation {
    [Serializable]
    public abstract class UntypedVariable {
        public virtual void Handle(TreeContext context) {
            if (!context.Contains(this)) {
                HandleInternal(context);
            }
        }
        public abstract void HandleInternal(TreeContext context);

    }
}