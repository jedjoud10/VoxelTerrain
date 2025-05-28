namespace jedjoud.VoxelTerrain.Generation {
    public class CustomCodeNode<T> : Variable<T> {
        public CustomCode.Callback callback;

        public override void HandleInternal(TreeContext ctx) {
            string result = callback?.Invoke(this, ctx);
            ctx.DefineAndBindNode<T>(this, "__", result);
        }
    }

    public class CustomCodeChainedNode : Variable<int> {
        public CustomCodeChainedNode last;
        public CustomCode.ChainCallback callback;

        public override void HandleInternal(TreeContext ctx) {
            last?.Handle(ctx);
            callback?.Invoke(this, ctx);
            ctx.BindNode<int>(this);
        }
    }

    public class CustomCode {
        public delegate string Callback(UntypedVariable self, TreeContext ctx);
        public delegate void ChainCallback(UntypedVariable self, TreeContext ctx);

        public static Variable<T> WithCode<T>(Callback callback) {
            return new CustomCodeNode<T> {
                callback = callback,
            };
        }

        public static CustomCodeChainedNode WithNext(CustomCodeChainedNode last, ChainCallback callback) {
            return new CustomCodeChainedNode {
                callback = callback,
                last = last
            };
        }
    }
}