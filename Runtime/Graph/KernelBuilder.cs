using System;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Generation {
    public class KernelBuilder {
        public string name;
        public ScopeArgument[] arguments;
        public Action<TreeContext> customCallback;
        public KernelDispatch dispatch;
        public int3 numThreads;
        public KeywordGuards dispatchGuards;
        public KeywordGuards scopeGuards;

        public void Build(TreeContext ctx) {
            // get rid of the 'CS' at the start
            string scopeName = name.Substring(2);

            int idx = ctx.scopes.Count;
            TreeScope scope = new TreeScope();
            ctx.currentScope = scope;
            ctx.scopes.Add(scope);
            ctx.scopes[idx].name = scopeName;
            ctx.scopes[idx].arguments = arguments;
            ctx.scopes[idx].keywordGuards = scopeGuards;

            foreach (var arg in arguments) {
                if (arg.output) {
                    arg.node.Handle(ctx);
                } else {
                    ctx.Add(arg.node, arg.name);
                }
            }

            customCallback?.Invoke(ctx);

            dispatch.name = name;
            dispatch.scopeName = scopeName;
            dispatch.scope = scope;
            dispatch.keywordGuards = dispatchGuards;
            dispatch.numThreads = numThreads;
            ctx.dispatches.Add(dispatch);
        }
    }
}