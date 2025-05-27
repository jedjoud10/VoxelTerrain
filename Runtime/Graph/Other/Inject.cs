using System;

namespace jedjoud.VoxelTerrain.Generation {
    public class InjectedNode<T> : Variable<T> {
        public Inject<T> a;
        public override void HandleInternal(TreeContext ctx) {
            ctx.Inject<T>(this, "inj", () => a.value);
        }
    }

    [Serializable]
    public class Inject<T> {
        public T value;

        public static implicit operator Inject<T>(T value) {
            return new Inject<T> { value = value };
        }
    }
}