using System;
using System.Collections.Generic;


namespace jedjoud.VoxelTerrain.Generation {
    public class ScopeArgument {
        public VariableType type;
        public UntypedVariable node;
        public string name;
        public bool output;

        public ScopeArgument(string name, VariableType type, UntypedVariable node, bool output) {
            this.type = type;
            this.name = name;
            this.node = node;
            this.output = output;
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
}