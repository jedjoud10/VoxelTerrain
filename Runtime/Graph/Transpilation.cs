using jedjoud.VoxelTerrain.Props;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    public partial class VoxelGraph : VoxelBehaviour {
        [HideInInspector]
        public TreeContext ctx;

        [SerializeField]
        private int hash;

        // Parses the voxel graph into a tree context with all required nodes and everything!!!
        public void ParsedTranspilation() {
            ctx = new TreeContext(debugName);
            ctx.scopes = new List<TreeScope>() {
                // Voxel density scope
                new TreeScope(0),

                // Prop generation scope
                new TreeScope(0),
            };
            ctx.Hash(debugName);

            // Create the external inputs that we use inside the function scope
            Variable<float3> position = new NoOp<float3>();
            var tempPos = new ScopeArgument("position", VariableType.StrictType.Float3, position, false);
            
            // Execute the voxel graph to get all required output variables 
            // We will contextualize the variables in their separate passes ({ density + color + material }, { props }, etc)
            AllInputs inputs = new AllInputs() { position = position };
            Execute(inputs, out AllOutputs outputs);
            ScopeArgument voxelArgument = new ScopeArgument("voxel", VariableType.StrictType.Float, outputs.density, true);
            ScopeArgument colorArgument = new ScopeArgument("color", VariableType.StrictType.Float3, outputs.color, true);
            ScopeArgument propArgument = new ScopeArgument("prop", VariableType.StrictType.Prop, outputs.prop, true);

            ctx.currentScope = 0;
            ctx.scopes[0].name = "Voxel";
            ctx.scopes[0].arguments = new ScopeArgument[] {
                tempPos, voxelArgument, colorArgument
            };
            ctx.Add(position, "position");
            outputs.density.Handle(ctx);
            outputs.color.Handle(ctx);

            
            ctx.currentScope = 1;
            ctx.Add(position, "position");
            ctx.scopes[1].name = "Props";
            ctx.scopes[1].arguments = new ScopeArgument[] {
                tempPos, propArgument
            };
            outputs.prop.Handle(ctx);


            // Voxel kernel dispatcher
            ctx.dispatches.Add(new KernelDispatch {
                name = $"CSVoxel",
                depth = 0,
                sizeReductionPower = 0,
                threeDimensions = true,
                scopeName = "Voxel",
                frac = 1.0f,
                scopeIndex = 0,
                mortonate = true,
                numThreads = new Vector3Int(8, 8, 8),
                remappedCoords = "id.xyz",
                writeCoords = "xyz",
                outputs = new KernelOutput[] {
                    new KernelOutput { output = voxelArgument, outputTextureName = "voxels" },
                    new KernelOutput { output = colorArgument, outputTextureName = "colors" },
                }
            });

            // Prop kernel dispatcher
            ctx.dispatches.Add(new KernelDispatch {
                name = $"CSProps",
                depth = 0,
                sizeReductionPower = 0,
                threeDimensions = true,
                scopeName = "Props",
                frac = 1.0f,
                scopeIndex = 1,
                mortonate = true,
                numThreads = new Vector3Int(8, 8, 8),
                remappedCoords = "id.xyz",
                writeCoords = "xyz",
                outputs = new KernelOutput[] {
                    new KernelOutput { output = propArgument, outputBufferName = "props", buffer = true }
                }
            });

            ctx.dispatches.Sort((KernelDispatch a, KernelDispatch b) => { return b.depth.CompareTo(a.depth); });
        }

        // This transpile the voxel graph into HLSL code that can be executed on the GPU
        // This can be done outside the editor, but shader compilation MUST be done in editor
        private string Transpile() {
            if (ctx == null) {
                ParsedTranspilation();
            } else {
            }

            List<string> lines = new List<string>();
            lines.AddRange(ctx.Properties);
            
            // Include all includes kek. Look in the file for more.
            lines.Add("#include \"Packages/com.jedjoud.voxelterrain/Runtime/Compute/Imports.cginc\"");
            var temp = ctx.dispatches.AsEnumerable().Select(x => x.ConvertToKernelString(ctx)).ToList();

            // Sort the scopes based on their depth
            // We want the scopes that don't require other scopes to be defined at the top, and scopes that require scopes to be defined at the bottom
            ctx.scopes.Sort((TreeScope a, TreeScope b) => { return b.depth.CompareTo(a.depth); });

            // Define each scope as a separate function with its arguments (input / output)
            int index = 0;
            foreach (var scope in ctx.scopes) {
                lines.Add($"// defined nodes: {scope.namesToNodes.Count}, depth: {scope.depth}, index: {index}, total lines: {scope.lines.Count}, argument count: {scope.arguments.Length} ");

                // Create a string containing all the required arguments and stuff
                string arguments = "";
                for (int i = 0; i < scope.arguments.Length; i++) {
                    var item = scope.arguments[i];
                    var comma = i == scope.arguments.Length - 1 ? "" : ",";
                    var output = item.output ? " out " : "";

                    arguments += $"{output}{item.type.ToStringType()} {item.name}{comma}";
                }

                // Open scope
                lines.Add($"void {scope.name}({arguments}) {{");

                // Set the output arguments inside of the scope
                foreach (var item in scope.arguments) {
                    if (item.output) {
                        scope.AddLine($"{item.name} = {scope.namesToNodes[item.node]};");
                    }
                }

                // Add the lines of the scope to the main shader lines
                IEnumerable<string> parsed2 = scope.lines.SelectMany(str => str.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None)).Select(x => $"{x}");
                lines.AddRange(parsed2);

                // Close scope
                lines.Add("}\n");
                index++;
            }

            lines.AddRange(temp);

            return lines.Aggregate("", (a, b) => a + "\n" + b);
        }
    }
}