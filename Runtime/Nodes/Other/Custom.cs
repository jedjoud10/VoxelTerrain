public class CustomCodeNode<T> : Variable<T> {
    public CustomCode<T>.Callback callback;

    public override void HandleInternal(TreeContext ctx) {
        callback?.Invoke(this, ctx);
    }
}

public class CustomCode<T> {
    public delegate void Callback(TreeNode self, TreeContext ctx);
    public Callback callback;
    
    public CustomCode(Callback callback) {
        this.callback = callback;
    }

    public Variable<T> DoStuff() {
        return new CustomCodeNode<T> {
            callback = callback,
        };
    }
}