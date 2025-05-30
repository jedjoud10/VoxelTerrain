using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

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
            ctx.currentScope = idx;
            ctx.scopes.Add(new TreeScope(0));
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
            dispatch.depth = 0;
            dispatch.scopeName = scopeName;
            dispatch.scopeIndex = idx;
            dispatch.keywordGuards = dispatchGuards;
            dispatch.numThreads = numThreads;
            ctx.dispatches.Add(dispatch);
        }
    }
}