using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
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

            if (GetComponent<ManagedTerrainGraph>() == null) {
                return;
            }

            Parse();

            if (hash != ctx.hash) {
                hash = ctx.hash;
                dirty = true;
            }
        }


        // Writes the transpiled shader code to a file and recompiles it automatically (through AssetDatabase)
        public void Compile(bool force) {
#if UNITY_EDITOR
            dirty = false;

            if (force) {
                ctx = null;
            }

            string source = Transpile();
            string name = this.gameObject.name.ToLower().Replace(' ', '_');

            if (!AssetDatabase.IsValidFolder("Assets/Voxel Terrain/Compute/")) {
                AssetDatabase.CreateFolder("Assets", "Voxel Terrain");
                AssetDatabase.CreateFolder("Assets/Voxel Terrain", "Compute");
            }

            string filePath = "Assets/Voxel Terrain/Compute/" + name + ".compute";
            string metaFilePath = "Assets/Voxel Terrain/Compute/" + name + ".compute.meta";

            using (StreamWriter sw = File.CreateText(filePath)) {
                sw.Write(source);
            }

            // fix this pls...
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh();

            shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(filePath);
            EditorUtility.SetDirty(gameObject);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();

            //UnityEngine.Experimental.Rendering.ShaderWarmup.WarmupShader(shader, new UnityEngine.Experimental.Rendering.ShaderWarmupSetup() { vdecl = null });

            if (shader == null) {
                Debug.LogWarning("wut?");
                return;
            }

            if (!gameObject.activeSelf)
                return;

#else
            throw new System.Exception("Cannot compile code at runtime");
#endif

            return;
        }

        // Parses the voxel graph into a tree context with all required nodes and everything!!!
        // TODO: PLEASE FOR THE LOVE OF GOD. PLEASE. PLEASE I BEG YOU PLEASE REWRITE THIS!!! THIS SHIT IS ASS!!!!!
        public void Parse() {
            ManagedTerrainGraph graph = GetComponent<ManagedTerrainGraph>();

            // TODO: for SOME fucking reason debug name causes problems
            // unity doesn't seem to be saving the debugNames field or doing some fucky fucky shit with it. pls fix
            ctx = new TreeContext(false);
            ctx.scopes = new List<TreeScope>();

            // Create the external inputs that we use inside the function scope
            ScopeArgument position = ScopeArgument.AsInput<float3>("position");

            // Run the graph for the voxels pass
            graph.Density((Variable<float3>)position.node, out Variable<float> density);

            // Create the scope and kernel for the voxel generation step
            // This will be used by the terrain previewer, terrain async readback system, and terrain segmentation system
            KernelBuilder voxelKernelBuilder = new KernelBuilder {
                name = "CSVoxels",
                arguments = new ScopeArgument[] {
                    position,
                    ScopeArgument.AsOutput<float>("density", density),
                    ScopeArgument.AsOutput<int>("material", 0)
                },
                dispatch = new VoxelKernelDispatch {
                },
                numThreads = new int3(8),
                dispatchGuards = new KeywordGuards(ComputeKeywords.OCTAL_READBACK, ComputeKeywords.PREVIEW, ComputeKeywords.SEGMENT_VOXELS),
                scopeGuards = null,
            };

            // Create a specific new node for sampling from the voxel texture
            Variable<float> cachedDensity = CustomCode.WithCode<float>((UntypedVariable self, TreeContext ctx) => {
                foreach (var (name, texture) in ctx.textures) {
                    if (texture.readKernels.Contains("CSVoxels")) {
                        texture.readKernels.Add($"CS{ctx.scopes[ctx.currentScope].name}");
                    }
                }
                
                return @$"DensityAtSlow({position.name})";
            });

            // Run the graph for the props pass
            ScopeArgument dispatch = ScopeArgument.AsInput<int>("dispatch");
            ScopeArgument type = ScopeArgument.AsInput<int>("type");
            ManagedTerrainGraph.PropInput propInput = new ManagedTerrainGraph.PropInput() {
                position = (Variable<float3>)position.node,
                density = cachedDensity,
                dispatch = (Variable<int>)dispatch.node,
                type = (Variable<int>)type.node
            };
            ManagedTerrainGraph.PropContext propContext = new ManagedTerrainGraph.PropContext((Variable<int>)dispatch.node);

            // Run the graph for the props pass
            graph.Props(propInput, propContext);

            // Create the scope and kernel for the prop generation step
            KernelBuilder propKernelBuilder = new KernelBuilder {
                name = "CSProps",
                arguments = new ScopeArgument[] {
                    position, dispatch, type,
                },
                customCallback = (TreeContext ctx) => {
                    cachedDensity.Handle(ctx);
                    propContext.chain?.Handle(ctx);
                },
                dispatch = new PropKernelDispatch {
                },
                dispatchGuards = new KeywordGuards(ComputeKeywords.SEGMENT_PROPS),
                scopeGuards = new KeywordGuards(ComputeKeywords.SEGMENT_PROPS),
                numThreads = new int3(64, 1, 1),
            };

            voxelKernelBuilder.Build(ctx);
            propKernelBuilder.Build(ctx);

            ctx.dispatches.Sort((KernelDispatch a, KernelDispatch b) => { return b.depth.CompareTo(a.depth); });
        }

        // Gotta add a check as it seems like adding the pragma just makes the shader un-compilable???
        public bool SupportsDXC() {
            return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12;
        }

        // This transpile the voxel graph into HLSL code that can be executed on the GPU
        // This can be done outside the editor, but shader compilation MUST be done in editor
        private string Transpile() {
            if (ctx == null) {
                Parse();
            }

            List<string> lines = new List<string>();
            lines.Add(ComputeKeywords.PRAGMA_MULTI_COMPILE);

            // DXC helps compile times by a LOT, albeit it isn't supported on DX11...
            // Gonna add a simple check instead, so that we fallback to FXC just in case
            if (SupportsDXC()) {
                lines.Add("#pragma use_dxc");
            }

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