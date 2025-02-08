using System;


[Serializable]
public abstract class TreeNode {
    public virtual void Handle(TreeContext context) {
        if (!context.Contains(this)) {
            HandleInternal(context);
        }
    }
    public abstract void HandleInternal(TreeContext context);

}