using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
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
        public bool dirty;

        public ComputeShader shader;

        // Every time the user updates a field, we will re-transpile (to check for hash-differences) and re-compile if needed
        // (that's what soft-recompilation is)
        private void OnValidate() {
            if (!gameObject.activeSelf)
                return;

            SoftRecompile();
            GetComponent<ManagedTerrainPreview>()?.OnPropertiesChanged();
        }

        // Checks if we need to recompile the shader by checking the hash changes.
        // If the context hash changed, then we will recompile the shader
        public void SoftRecompile() {
            if (!gameObject.activeSelf)
                return;

            ParsedTranspilation();

            if (hash != ctx.hash) {
                hash = ctx.hash;
                dirty = true;
            }
        }


        // Writes the transpiled shader code to a file and recompiles it automatically (through AssetDatabase)
        public void Compile(bool force) {
#if UNITY_EDITOR
            dirty = false;
            //GetComponent<VoxelExecutor>().DisposeResources();

            if (force) {
                ctx = null;
            }

            string source = Transpile();
            string name = this.gameObject.name.ToLower().Replace(' ', '_');

            if (!AssetDatabase.IsValidFolder("Assets/Voxel Terrain/Compute/")) {
                // TODO: Use package cache instead? would it work???
                AssetDatabase.CreateFolder("Assets", "Voxel Terrain");
                AssetDatabase.CreateFolder("Assets/Voxel Terrain", "Compute");
            }

            string filePath = "Assets/Voxel Terrain/Compute/" + name + ".compute";
            string metaFilePath = "Assets/Voxel Terrain/Compute/" + name + ".compute.meta";

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
        // TODO: PLEASE FOR THE LOVE OF GOD. PLEASE. PLEASE I BEG YOU PLEASE REWRITE THIS!!! THIS SHIT IS ASS!!!!!
        public void ParsedTranspilation() {
            ManagedTerrainGraph graph = GetComponent<ManagedTerrainGraph>();

            if (graph == null) {
                Debug.LogError("Can't transpile the graph since we don't have one to begin with! Add a ManagedTerrainGraph component...");
                return;
            }

            // TODO: for SOME fucking reason debug name causes problems
            // unity doesn't seem to be saving the debugNames field or doing some fucky fucky shit with it. pls fix
            ctx = new TreeContext(false);
            ctx.scopes = new List<TreeScope>();

            // Create the external inputs that we use inside the function scope
            ScopeArgument position = ScopeArgument.AsInput<float3>("position");
            //ScopeArgument id = ScopeArgument.AsInput<int3>("id");

            // Run the graph for the voxels pass
            ManagedTerrainGraph.VoxelInput voxelInput = new ManagedTerrainGraph.VoxelInput() { position = (Variable<float3>)position.node };
            graph.Voxels(voxelInput, out ManagedTerrainGraph.VoxelOutput voxelOutput);

            // Create the scope and kernel for the voxel generation step
            // This will be used by the terrain previewer, terrain async readback system, and terrain segmentation system
            KernelBuilder voxelKernelBuilder = new KernelBuilder {
                name = "CSVoxels",
                arguments = new ScopeArgument[] {
                    position,
                    ScopeArgument.AsOutput<float>("voxel", voxelOutput.density),
                    ScopeArgument.AsOutput<int>("material", 0)
                },
                dispatch = new VoxelKernelDispatch {
                },
                numThreads = new int3(8),
                keywordGuards = new KeywordGuards(ComputeKeywords.OCTAL_READBACK, ComputeKeywords.PREVIEW, ComputeKeywords.SEGMENT_VOXELS),
            };

            // Create a specific new node for sampling from the voxel texture
            Variable<float> cachedDensity = CustomCode.WithCode<float>((UntypedVariable self, TreeContext ctx) => {
                return @$"DensityAt({position.name})";
            });

            // Run the graph for the props pass
            ManagedTerrainGraph.PropInput propInput = new ManagedTerrainGraph.PropInput() {
                position = (Variable<float3>)position.node,
                density = cachedDensity,
                normal = float3.zero,
            };
            ManagedTerrainGraph.PropContext propContext = new ManagedTerrainGraph.PropContext();

            // Run the graph for the props pass
            graph.Props(propInput, propContext);

            // Create the scope and kernel for the prop generation step
            KernelBuilder propKernelBuilder = new KernelBuilder {
                name = "CSProps",
                arguments = new ScopeArgument[] {
                    position,
                },
                customCallback = (TreeContext ctx) => {
                    cachedDensity.Handle(ctx);
                    propContext.chain.Handle(ctx);
                },
                dispatch = new PropKernelDispatch {
                },
                keywordGuards = new KeywordGuards(ComputeKeywords.SEGMENT_PROPS),
                numThreads = new int3(32, 1, 1),
            };

            voxelKernelBuilder.Build(ctx);
            propKernelBuilder.Build(ctx);

            ctx.dispatches.Sort((KernelDispatch a, KernelDispatch b) => { return b.depth.CompareTo(a.depth); });
        }

        // This transpile the voxel graph into HLSL code that can be executed on the GPU
        // This can be done outside the editor, but shader compilation MUST be done in editor
        private string Transpile() {
            if (ctx == null) {
                ParsedTranspilation();
            }

            List<string> lines = new List<string>();
            lines.Add(ComputeKeywords.PRAGMA_MULTI_COMPILE);

            lines.AddRange(ctx.Properties);
            lines.Add("#include \"Packages/com.jedjoud.voxelterrain/Runtime/Compute/Imports.cginc\"");

            // Sort the scopes based on their depth
            // We want the scopes that don't require other scopes to be defined at the top, and scopes that require scopes to be defined at the bottom
            ctx.scopes.Sort((TreeScope a, TreeScope b) => { return b.depth.CompareTo(a.depth); });

            // Define each scope as a separate function with its arguments (input / output)
            for (int i = 0; i < ctx.scopes.Count; i++) {
                TreeScope scope = ctx.scopes[i];
                lines.AddRange(scope.CreateScope(i));
            }

            // Create the dispatches
            lines.AddRange(ctx.dispatches.AsEnumerable().Select(x => x.CreateKernel(ctx)).ToList());

            return lines.Aggregate("", (a, b) => a + "\n" + b);
        }
    }
}