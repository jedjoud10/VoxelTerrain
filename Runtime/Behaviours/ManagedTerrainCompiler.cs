using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

using System;
using System.IO;



#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jedjoud.VoxelTerrain.Generation {
    public class ManagedTerrainCompiler : MonoBehaviour {
        [HideInInspector]
        public TreeContext ctx;

        [SerializeField]
        private int hash;

        [HideInInspector]
        public int voxelsDispatchIndex;

        [HideInInspector]
        public int propsDispatchIndex;
        public RenderTexture debugTex;
        public ComputeShader shader;

        // Every time the user updates a field, we will re-transpile (to check for hash-differences) and re-compile if needed
        // (that's what soft-recompilation is)
        private void OnValidate() {
            if (!gameObject.activeSelf)
                return;

            SoftRecompile();
            OnPropertiesChanged();
        }

        // Called when the voxel graph's properties get modified
        public void OnPropertiesChanged() {
            if (!gameObject.activeSelf)
                return;

            /*
#if UNITY_EDITOR
            var visualizer = GetComponent<VoxelPreview>();
            if (visualizer != null && visualizer.isActiveAndEnabled) {
                var exec = GetComponent<VoxelExecutor>();

                VoxelExecutor.EditorPreviewParameters parameters = new VoxelExecutor.EditorPreviewParameters() {
                    newSize = visualizer.size,
                    previewScale = visualizer.scale,
                    previewOffset = visualizer.offset,
                    dispatchIndex = this.voxelsDispatchIndex,
                    updateInjected = true,
                };

                exec.ExecuteShader(parameters);
                RenderTexture voxels = (RenderTexture)exec.textures["voxels"];
                debugTex = voxels;
                visualizer.Meshify(voxels);
            }
#endif
            */
        }

        // Checks if we need to recompile the shader by checking the hash changes.
        // If the context hash changed, then we will recompile the shader
        public void SoftRecompile() {
            if (!gameObject.activeSelf)
                return;

            ParsedTranspilation();

            /*
            if (hash != ctx.hashinator.hash) {
                hash = ctx.hashinator.hash;

                GetComponent<VoxelExecutor>().DisposeResources();

                if (autoCompile) {
                    Compile(false);
                }
            }
            */
        }


        // Writes the transpiled shader code to a file and recompiles it automatically (through AssetDatabase)
        public void Compile(bool force) {
#if UNITY_EDITOR
            //GetComponent<VoxelExecutor>().DisposeResources();

            if (force) {
                ctx = null;
            }

            string source = Transpile();
            //Debug.Log(source);

            if (!AssetDatabase.IsValidFolder("Assets/Voxel Terrain/Compute/")) {
                // TODO: Use package cache instead? would it work???
                AssetDatabase.CreateFolder("Assets", "Voxel Terrain");
                AssetDatabase.CreateFolder("Assets/Voxel Terrain", "Compute");
            }

            string filePath = "Assets/Voxel Terrain/Compute/" + name.ToLower() + ".compute";
            string metaFilePath = "Assets/Voxel Terrain/Compute/" + name.ToLower() + ".compute.meta";

            /*
            if (File.Exists(filePath)) {
                AssetDatabase.DeleteAsset(filePath);
            }
            */

            using (StreamWriter sw = File.CreateText(filePath)) {
                sw.Write(source);
            }

            AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(filePath);

            if (shader == null) {
                Debug.LogWarning("wut?");
                return;
            }

            if (!gameObject.activeSelf)
                return;

            /*
            var visualizer = GetComponent<VoxelPreview>();
            visualizer?.InitializeForSize();
            */
#else
            throw new System.Exception("Cannot compile code at runtime");
#endif

            return;
        }

#if UNITY_EDITOR
        // Recompiles the graph every time we reload the domain
        [InitializeOnLoadMethod]
        static void RecompileOnDomainReload() {

            /*
            VoxelGenerator[] graph = Object.FindObjectsByType<VoxelGenerator>(FindObjectsSortMode.None);
            foreach (var item in graph) {
                //item.Compile();
            }
            */
        }
#endif

        // Parses the voxel graph into a tree context with all required nodes and everything!!!
        public void ParsedTranspilation() {
            ManagedTerrainGraph graph = GetComponent<ManagedTerrainGraph>();

            if (graph == null) {
                Debug.LogError("Can't transpile the graph since we don't have one to begin with! Add a VoxelGraph component...");
                return;
            }

            // TODO: for SOME FUCKING reason this causes problems
            // unity doesn't seem to be saving the debugNames field or doing some fucky fucky shit with it. pls fix
            ctx = new TreeContext(false);
            ctx.scopes = new List<TreeScope>() {
                // Voxel density scope
                new TreeScope(0),

                // Prop generation scope
                //new TreeScope(0),
            };
            voxelsDispatchIndex = 0;
            propsDispatchIndex = -1;

            // Create the external inputs that we use inside the function scope
            Variable<float3> position = new NoOp<float3>();
            var tempPos = new ScopeArgument("position", VariableType.StrictType.Float3, position, false);
            Variable<int3> id = new NoOp<int3>();
            var tempId = new ScopeArgument("id", VariableType.StrictType.Int3, id, false);
            ctx.position = tempPos;
            ctx.id = tempId;

            // Execute the voxel graph to get all required output variables 
            // We will contextualize the variables in their separate passes ({ density + color + material }, { props }, etc)
            ManagedTerrainGraph.AllInputs inputs = new ManagedTerrainGraph.AllInputs() { position = position, id = id };
            graph.Execute(inputs, out ManagedTerrainGraph.AllOutputs outputs);
            ScopeArgument voxelArgument = new ScopeArgument("voxel", VariableType.StrictType.Float, outputs.density, true);
            //ScopeArgument propArgument = new ScopeArgument("prop", VariableType.StrictType.Prop, outputs.prop, true);
            ScopeArgument materialArgument = new ScopeArgument("material", VariableType.StrictType.Int, outputs.material, true);

            ctx.currentScope = 0;
            ctx.scopes[0].name = "Voxel";
            ctx.scopes[0].arguments = new ScopeArgument[] {
                tempPos, tempId, voxelArgument, materialArgument
            };
            ctx.Add(position, "position");
            outputs.density.Handle(ctx);
            outputs.material.Handle(ctx);

            /*
            ctx.currentScope = 1;
            ctx.Add(position, "position");
            ctx.scopes[1].name = "Props";
            ctx.scopes[1].arguments = new ScopeArgument[] {
                tempPos, tempId, propArgument
            };
            outputs.prop.Handle(ctx);
            */


            // Voxel kernel dispatcher
            ctx.dispatches.Add(new VoxelKernelDispatch {
                name = $"CSVoxel",
                depth = 0,
                scopeName = "Voxel",
                scopeIndex = 0,
                outputs = new KernelOutput[] {
                    //new KernelOutput { setter = "packVoxelData(voxel, material)", outputTextureName = "voxels" },
                    new KernelOutput { setter = "packVoxelData(voxel, material)", outputBufferName = "voxels", buffer = true }
                }
            });

            /*
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
                    new KernelOutput { setter = "prop", outputBufferName = "props", buffer = true }
                }
            });
            */

            ctx.dispatches.Sort((KernelDispatch a, KernelDispatch b) => { return b.depth.CompareTo(a.depth); });
        }

        // This transpile the voxel graph into HLSL code that can be executed on the GPU
        // This can be done outside the editor, but shader compilation MUST be done in editor
        private string Transpile() {
            if (ctx == null) {
                ParsedTranspilation();
            }

            List<string> lines = new List<string>();

            // Add the octal async readback pragma
            lines.Add("#pragma multi_compile __ _ASYNC_READBACK_OCTAL\n");

            lines.AddRange(ctx.Properties);


            // Include all includes kek. Look in the file for more.
            lines.Add("#include \"Packages/com.jedjoud.voxelterrain/Runtime/Compute/Imports.cginc\"");
            var temp = ctx.dispatches.AsEnumerable().Select(x => x.CreateKernel(ctx)).ToList();

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
                        if (item.node == null) {
                            throw new NullReferenceException($"Output argument '{item.name}' was not set in the graph");
                        }

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