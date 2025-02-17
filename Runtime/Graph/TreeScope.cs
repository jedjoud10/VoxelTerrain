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

        public string InitializeVariables() {
            string kernelOutputTemp = "";

            for (int i = 0; i < arguments.Length; i++) {
                var item = arguments[i];
                var newLine = i == arguments.Length - 1 ? "" : "\n";
                if (item.output) {
                    kernelOutputTemp += $"    {item.type.ToStringType()} {item.name};{newLine}";
                }
            }

            return kernelOutputTemp;
        }

        public string GenerateScopedArguments() {
            string output = "";
            for (int i = 0; i < arguments.Length; i++) {
                var item = arguments[i];
                var comma = i == arguments.Length - 1 ? "" : ",";
                output += $"{item.name}{comma}";
            }

            return $"    {name}({output});";
        }
    }
}