using System;
using System.Collections.Generic;
using System.Linq;


namespace jedjoud.VoxelTerrain.Generation {
    public class TreeScope {
        public KeywordGuards keywordGuards = null;
        public List<string> lines;
        public Dictionary<UntypedVariable, string> nodesToNames;
        public int depth;

        // TODO: this should NOT be stored here. "arguments" is also a wrong name for this, as this stores parameter data as well (incoming variable names from outside the scope)
        // what TreeScope should store is just proper "arguments" (name and type, nothin related to actual nodes). Everything else (parameters) should be given from the outside
        public ScopeArgument[] arguments;
        public string name;
        public int indent;

        public TreeScope(int depth) {
            this.lines = new List<string>();
            this.nodesToNames = new Dictionary<UntypedVariable, string>();
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


        public List<string> CreateScope(int scopeIndex) {
            List<string> outputLines = new List<string>();
            outputLines.Add($"// defined nodes: {nodesToNames.Count}, depth: {depth}, index: {scopeIndex}, total lines: {lines.Count}, argument count: {arguments.Length} ");

            // Create a string containing all the required arguments and stuff
            string argumentsCode = "";
            for (int i = 0; i < arguments.Length; i++) {
                var item = arguments[i];

                if (item.node == null) {
                    throw new NullReferenceException($"Input argument '{item.name}' is null");
                }

                var comma = i == arguments.Length - 1 ? "" : ",";
                var output = item.output ? " out " : "";

                argumentsCode += $"{output}{item.type.ToStringType()} {item.name}{comma}";
            }

            // Open scope
            outputLines.Add($"void {name}({argumentsCode}) {{");

            // Add the lines of the scope to the main shader lines
            IEnumerable<string> parsed2 = lines.SelectMany(str => str.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None)).Select(x => $"{x}");
            outputLines.AddRange(parsed2);

            // Set the output arguments inside of the scope
            foreach (var item in arguments) {
                if (item.output) {
                    if (item.node == null) {
                        throw new NullReferenceException($"Output argument '{item.name}' is null");
                    }

                    outputLines.Add($"    {item.name} = {nodesToNames[item.node]};");
                }
            }

            // Close scope
            outputLines.Add("}");


            // Add keyword guards if needed
            if (keywordGuards != null) {
                outputLines.Insert(0, keywordGuards.BeginGuard());
                outputLines.Add(keywordGuards.EndGuard());
            }

            return outputLines;
        }
    }
}