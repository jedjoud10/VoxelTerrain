using System;


public class InjectedNode<T> : Variable<T> {
    public Inject<T> a;
    public override void HandleInternal(TreeContext ctx) {
        ctx.Inject<T>(this, "inj", () => a.x);
    }
}

[Serializable]
public class Inject<T> {
    public T x;

    public Inject(T a) {
        this.x = a;
    }
}