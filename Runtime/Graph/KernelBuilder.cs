using System;
using System.Collections.Generic;

namespace jedjoud.VoxelTerrain.Generation {
    public class KernelBuilder {
        public string dispatchIndexIdentifier;
        public string scopeName;
        public ScopeArgument[] arguments;
        public Action<TreeContext> customCallback;
        public KernelDispatch dispatch;
        public KeywordGuards keywordGuards;

        public void Build(TreeContext ctx, Dictionary<string, int> dispatchIndices) {
            int idx = ctx.scopes.Count;
            ctx.currentScope = idx;
            ctx.scopes.Add(new TreeScope(0));
            ctx.scopes[idx].name = scopeName;
            ctx.scopes[idx].arguments = arguments;
            ctx.scopes[idx].keywordGuards = keywordGuards;

            foreach (var arg in arguments) {
                if (arg.output) {
                    arg.node.Handle(ctx);
                } else {
                    ctx.Add(arg.node, arg.name);
                }
            }

            customCallback?.Invoke(ctx);

            dispatch.name = $"CS{scopeName}";
            dispatch.depth = 0;
            dispatch.scopeName = scopeName;
            dispatch.scopeIndex = idx;
            dispatch.keywordGuards = keywordGuards;

            int dspIdx = ctx.dispatches.Count;
            ctx.dispatches.Add(dispatch);
            dispatchIndices.Add(dispatchIndexIdentifier, dspIdx);
        }
    }
}