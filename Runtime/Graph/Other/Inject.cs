using System;

namespace jedjoud.VoxelTerrain.Generation {
    [Serializable]
    public class Inject<T> : Variable<T> {
        public T value;
        public override void HandleInternal(TreeContext ctx) {
            ctx.Inject<T>(this, "inj", () => value);
        }

        public static implicit operator Inject<T>(T value) {
            return new Inject<T> { value = value };
        }
    }
}