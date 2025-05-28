using System;
using System.Collections.Generic;


namespace jedjoud.VoxelTerrain.Generation {
    public class ScopeArgument {
        public VariableType type;
        public UntypedVariable node;
        public string name;
        public bool output;

        public static ScopeArgument AsInput<T>(string name, Variable<T> backing = null) {
            ScopeArgument arg = new ScopeArgument();
            arg.type = VariableType.TypeOf<T>();
            arg.name = name;
            arg.node = backing ?? new NoOp<T>();
            arg.output = false;
            return arg;
        }

        public static ScopeArgument AsOutput<T>(string name, Variable<T> backing) {
            ScopeArgument arg = new ScopeArgument();
            arg.type = VariableType.TypeOf<T>();
            arg.name = name;
            arg.node = backing;
            arg.output = true;
            return arg;
        }
    }

    // One scope per compute shader kernel.
    // Multiple scopes are used when we want to execute multiple kernels sequentially
    public class TreeScope {
        public List<string> lines;
        public Dictionary<UntypedVariable, string> namesToNodes;
        public int depth;

        // TODO: this should NOT be stored here. "arguments" is also a wrong name for this, as this stores parameter data as well (incoming variable names from outside the scope)
        // what TreeScope should store is just proper "arguments" (name and type, nothin related to actual nodes). Everything else (parameters) should be given from the outside
        public ScopeArgument[] arguments;
        public string name;
        public int indent;

        public TreeScope(int depth) {
            this.lines = new List<string>();
            this.namesToNodes = new Dictionary<UntypedVariable, string>();
            this.indent = 1;
            this.depth = depth;
            this.arguments = null;
            this.name = "TreeScopeNameWasNotSet!!!";
        }
        public void AddLine(string line) {
            lines.Add(new string('\t', indent) + line);
        }

        // Initialize the variables that we will use as arguments
        public string InitArgVars((int, ScopeArgument)[] overwriteArgs = null) {
            ScopeArgument[] args = new ScopeArgument[arguments.Length];
            Array.Copy(arguments, args, arguments.Length);

            if (overwriteArgs != null) {
                foreach ((int index, ScopeArgument newArgument) in overwriteArgs) {
                    args[index] = newArgument;
                }
            }

            string kernelOutputTemp = "";

            for (int i = 0; i < args.Length; i++) {
                var item = args[i];
                var newLine = i == args.Length - 1 ? "" : "\n";
                if (item.output) {
                    kernelOutputTemp += $"    {item.type.ToStringType()} {item.name};{newLine}";
                }
            }

            return kernelOutputTemp;
        }

        // Calls the functions with the arguments that we setup
        public string CallWithArgs((int, ScopeArgument)[] overwriteArgs = null) {
            ScopeArgument[] args = new ScopeArgument[arguments.Length];
            Array.Copy(arguments, args, arguments.Length);

            if (overwriteArgs != null) {
                foreach ((int index, ScopeArgument newArgument) in overwriteArgs) {
                    args[index] = newArgument;
                }
            }

            string output = "";
            for (int i = 0; i < args.Length; i++) {
                var item = args[i];
                var comma = i == args.Length - 1 ? "" : ",";
                output += $"{item.name}{comma}";
            }

            return $"    {name}({output});";
        }
    }

    public class TreeScopeAndKernelBuilder {
        public string dispatchIndexIdentifier;
        public string scopeName;
        public CustomCodeChainedNode customCodeChain;
        public ScopeArgument[] arguments;
        public KernelDispatch dispatch;

        public void Build(TreeContext ctx, Dictionary<string, int> dispatchIndices) {
            int idx = ctx.scopes.Count;
            ctx.currentScope = idx;
            ctx.scopes.Add(new TreeScope(0));
            ctx.scopes[idx].name = scopeName;
            ctx.scopes[idx].arguments = arguments;

            foreach (var arg in arguments) {
                if (arg.output) {
                    arg.node.Handle(ctx);
                } else {
                    ctx.Add(arg.node, arg.name);
                }
            }

            if (customCodeChain != null) {
                customCodeChain.Handle(ctx);
            }

            dispatch.name = $"CS{scopeName}";
            dispatch.depth = 0;
            dispatch.scopeName = scopeName;
            dispatch.scopeIndex = idx;

            int dspIdx = ctx.dispatches.Count;
            ctx.dispatches.Add(dispatch);
            dispatchIndices.Add(dispatchIndexIdentifier, dspIdx);
        }
    }
}