using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEditor.Graphs;




#if UNITY_EDITOR
using UnityEditor;
#endif

// A voxel graph is the base class to inherit from to be able to write custom voxel stuff
public abstract partial class VoxelGenerator : VoxelBehaviour {
    [Header("Compilation")]
    public bool debugName = true;
    public bool autoCompile = true;

    public ComputeShader shader;
    private TreeContext ctx;
    private int hash;

    // Called when the voxel graph's properties get modified
    public void OnPropertiesChanged() {
        if (!gameObject.activeSelf)
            return;
        
        var visualizer = GetComponent<VoxelPreview>();

        if (visualizer != null) {
            ExecuteShader(visualizer.size, Vector3.zero, Vector3.one, false, true);
            RenderTexture density = (RenderTexture)textures["voxels"];
            RenderTexture colors = (RenderTexture)textures["colors"];
            //RenderTexture uvs = (RenderTexture)executor.Textures["uvs"];
            visualizer.Meshify(density, colors);
        }
    }

    // Called when the voxel graph gets recompiled in the editor
    public void OnRecompilation() {
        if (!gameObject.activeSelf)
            return;

        var visualizer = GetComponent<VoxelPreview>();
        visualizer?.InitializeForSize();
    }

    public class AllInputs {
        public Variable<float3> position;
    }

    public class AllOutputs {
        public Variable<float> density;
        public Variable<float3> color;
        public Variable<float> metallic;
        public Variable<float> smoothness;
    }

    // Execute the voxel graph at a specific position and fetch the density and material values
    public abstract void Execute(Variable<float3> position, out Variable<float> density, out Variable<float3> color);

    // Even lower execution function that allows you to override metallic and smoothness values (and even probably pass your own uv values if needed)
    public virtual void ExecuteWithEverything(AllInputs input, out AllOutputs output) {
        output = new AllOutputs();
        output.metallic = 0.0f;
        output.smoothness = 0.0f;
        Execute(input.position, out output.density, out output.color);
    }

    // Parses the voxel graph into a tree context with all required nodes and everything!!!
    private void ParsedTranspilation() {
        ctx = new TreeContext(debugName);

        // Create the external inputs that we use inside the function scope
        Variable<float3> position = ctx.AliasExternalInput<float3>("position");

        // Input scope arguments
        ctx.position = new ScopeArgument("position", GraphUtils.StrictType.Float3, position, false);
        
        // Execute the voxel graph to get density and color
        AllInputs inputs = new AllInputs() { position = position };
        ExecuteWithEverything(inputs, out AllOutputs outputs);

        var combinedUvs = new ConstructNode<float2>() { inputs = new Variable<float>[2] { outputs.smoothness, outputs.smoothness } };

        // Voxel function output arguments 
        ScopeArgument voxelArgument = new ScopeArgument("voxel", GraphUtils.StrictType.Float, outputs.density, true);
        ScopeArgument colorArgument = new ScopeArgument("color", GraphUtils.StrictType.Float3, outputs.color, true);
        ScopeArgument uvsArgument = new ScopeArgument("uvs", GraphUtils.StrictType.Float2, combinedUvs, true);

        // Voxel function scope
        // We can't initialize the scope again because it contains the shader graph nodes
        ctx.scopes[0].name = "Voxel";
        ctx.scopes[0].arguments = new ScopeArgument[] {
            ctx.position, voxelArgument, colorArgument
        };

        // Voxel kernel dispatcher
        ctx.dispatches.Add(new KernelDispatch {
            name = $"CSVoxel",
            depth = 0,
            sizeReductionPower = 0,
            threeDimensions = true,
            scopeName = "Voxel",
            frac = 1.0f,
            scopeIndex = 0,
            numThreads = "[numthreads(8, 8, 8)]",
            remappedCoords = "id.xyz",
            writeCoords = "xyz",
            morton = true,
            outputs = new KernelOutput[] {
                new KernelOutput { output = voxelArgument, outputTextureName = "voxels" },
                new KernelOutput { output = colorArgument, outputTextureName = "colors" },
                //new KernelOutput { output = uvsArgument, outputTextureName = "uvs" },
            }
        });

        // Prase the voxel graph going from density and color
        System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
        timer.Start();
        ctx.Parse(new TreeNode[] { outputs.density, outputs.color });
        timer.Stop();
        //Debug.Log($"{timer.Elapsed.TotalMilliseconds}ms");
        
        ctx.dispatches.Sort((KernelDispatch a, KernelDispatch b) => { return b.depth.CompareTo(a.depth); });
    }

    // This transpile the voxel graph into HLSL code that can be executed on the GPU
    // This can be done outside the editor, but shader compilation MUST be done in editor
    public string Transpile() {
        ParsedTranspilation();

        List<string> lines = new List<string>();
        lines.AddRange(ctx.Properties);
        lines.Add("RWTexture3D<float> voxels_write;");
        lines.Add("RWTexture3D<float3> colors_write;");
        lines.Add("RWTexture3D<float2> uvs_write;");

        lines.Add("int size;");
        lines.Add("int morton;");
        lines.Add("int3 permuationSeed;\nint3 moduloSeed;");
        lines.Add("float3 scale;\nfloat3 offset;");

        // imports
        //lines.Add("#include \"Assets/Compute/Noises.cginc\"");
        lines.Add("#include \"Packages/com.jedjoud.voxelterrain/Runtime/Compute/Noises.cginc\"");
        lines.Add("#include \"Packages/com.jedjoud.voxelterrain/Runtime/Compute/SDF.cginc\"");
        lines.Add("#include \"Packages/com.jedjoud.voxelterrain/Runtime/Compute/Other.cginc\"");

        lines.Add(@"
float3 ConvertIntoWorldPosition(float3 tahini) {
    //return  (tahini + offset) * scale;
    //return (tahini - 1.5f) * scale + offset;
    return (tahini * scale) + offset;
}

float3 ConvertFromWorldPosition(float3 worldPos) {
    return  (worldPos / scale) - offset;
}");
        lines.Add(@"
// Morton encoding from
// Stolen from https://github.com/johnsietsma/InfPoints/blob/master/com.infpoints/Runtime/Morton.cs
uint part1By2_32(uint x)
{
    x &= 0x3FF;  // x = ---- ---- ---- ---- ---- --98 7654 3210
    x = (x ^ (x << 16)) & 0xFF0000FF;  // x = ---- --98 ---- ---- ---- ---- 7654 3210
    x = (x ^ (x << 8)) & 0x300F00F;  // x = ---- --98 ---- ---- 7654 ---- ---- 3210
    x = (x ^ (x << 4)) & 0x30C30C3;  // x = ---- --98 ---- 76-- --54 ---- 32-- --10
    x = (x ^ (x << 2)) & 0x9249249;  // x = ---- 9--8 --7- -6-- 5--4 --3- -2-- 1--0
    return x;
}

uint encodeMorton32(uint3 coordinate)
{
    return (part1By2_32(coordinate.z) << 2) + (part1By2_32(coordinate.y) << 1) + part1By2_32(coordinate.x);
}


// taken from the voxels utils class
uint3 indexToPos(uint index)
{
    // N(ABC) -> N(A) x N(BC)
    uint y = index / (size * size);   // x in N(A)
    uint w = index % (size * size);  // w in N(BC)

    // N(BC) -> N(B) x N(C)
    uint z = w / size;// y in N(B)
    uint x = w % size;        // z in N(C)
    return uint3(x, y, z);
}
");
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

                arguments += $"{output}{GraphUtils.ToStringType(item.type)} {item.name}{comma}";
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

    // Checks if we need to recompile the shader by checking the hash changes. Calls a property callback in all cases
    public void SoftRecompile() {
        if (!gameObject.activeSelf)
            return;

        ParsedTranspilation();
        if (hash != ctx.hashinator.hash && autoCompile) {
            hash = ctx.hashinator.hash;
            Debug.Log("Hash changed, recompiling...");
            Compile();
        }
    }

    // Every time the user updates a field, we will re-transpile (to check for hash-differences) and re-compile if needed
    // Also executing the shader at the specified size as well
    private void OnValidate() {
        if (!gameObject.activeSelf)
            return;

        SoftRecompile();
        OnPropertiesChanged();
        ComputeSecondarySeeds();
    }

    // Transpiles the C# shader code and saves it to a compute shader file
    public void Compile() {
#if UNITY_EDITOR
        string source = Transpile();

        if (!AssetDatabase.IsValidFolder("Assets/Voxel Terrain/Compute/")) {
            // TODO: Use package cache instead? would it work???
            AssetDatabase.CreateFolder("Assets", "Voxel Terrain");
            AssetDatabase.CreateFolder("Assets/Voxel Terrain", "Compute");
            Debug.Log("Creating converted compute shaders folders");
        }

        string filePath = "Assets/Voxel Terrain/Compute/" + name.ToLower() + ".compute";
        using (StreamWriter sw = File.CreateText(filePath)) {
            sw.Write(source);
        }

        AssetDatabase.ImportAsset(filePath);
        shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(filePath);
        OnRecompilation();
        OnPropertiesChanged();
#else
            Debug.LogError("Cannot transpile code at runtime");
#endif
    }

#if UNITY_EDITOR
    // Recompiles the graph every time we reload the domain
    [InitializeOnLoadMethod]
    static void RecompileOnDomainReload() {
        VoxelGenerator[] graph = Object.FindObjectsByType<VoxelGenerator>(FindObjectsSortMode.None);

        foreach (var item in graph) {
            //item.Compile();
        }
    }
#endif


    [Header("Seeding")]
    public int seed = 1234;
    public Vector3Int permutationSeed;
    public Vector3Int moduloSeed;
    private Dictionary<string, ExecutorTexture> textures;
    private int setSize;

    // Create intermediate textures (cached, gradient) to be used for voxel graph shader execution
    // Texture size will correspond to execution size property
    private void CreateIntermediateTextures(int size) {
        // Dispose of previous render textures if needed
        if (textures != null) {
            foreach (var (name, tex) in textures) {
                if (tex.texture is RenderTexture casted) {
                    casted.Release();
                }
            }
        }

        // Creates dictionary with the default voxel graph textures (density + custom data)
        textures = new Dictionary<string, ExecutorTexture> {
            { "voxels", new OutputExecutorTexture("voxels", new List<string>() { "CSVoxel" }, GraphUtils.Create3DRenderTexture(size, GraphicsFormat.R16_SFloat)) },
            { "colors", new OutputExecutorTexture("colors", new List<string>() { "CSVoxel" }, GraphUtils.Create3DRenderTexture(size, GraphicsFormat.R8G8B8A8_UNorm)) },
            //{ "uvs", new OutputExecutorTexture("uvs", new List<string>() { "CSVoxel" }, Utils.Create3DRenderTexture(size, GraphicsFormat.R8G8_UNorm))},
        };

        foreach (var (name, descriptor) in ctx.textures) {
            textures.Add(name, descriptor.Create(size));
        }
    }

    public void ExecuteShader(int newSize, Vector3 offset, Vector3 scale, bool morton, bool updateInjected = true) {
        if (ctx == null) {
            ParsedTranspilation();
        }

        if (newSize != setSize || textures == null) {
            CreateIntermediateTextures(newSize);
        }

        shader.SetInt("size", newSize);
        shader.SetInts("permuationSeed", new int[] { permutationSeed.x, permutationSeed.y, permutationSeed.z });
        shader.SetInts("moduloSeed", new int[] { moduloSeed.x, moduloSeed.y, moduloSeed.z });
        shader.SetVector("offset", offset);
        shader.SetVector("scale", scale);
        shader.SetBool("morton", morton);

        if (updateInjected) {
            ctx.injector.UpdateInjected(shader, textures);
        }

        foreach (var (name, texture) in textures) {
            texture.BindToComputeShader(shader);
        }

        // Execute the kernels sequentially
        foreach (var kernel in ctx.dispatches) {
            int id = shader.FindKernel(kernel.name);
            int tempSize = newSize / (1 << kernel.sizeReductionPower);
            tempSize = Mathf.Max(tempSize, 1);

            int minScaleBase3D = Mathf.CeilToInt((float)tempSize / 8.0f);
            int minScaleBase2D = Mathf.CeilToInt((float)tempSize / 32.0f);

            if (kernel.threeDimensions) {
                shader.Dispatch(id, minScaleBase3D, minScaleBase3D, minScaleBase3D);
            } else {
                shader.Dispatch(id, minScaleBase2D, minScaleBase2D, 1);
            }

            // TODO: Dictionary<string, string> kernelsToWriteTexture = new Dictionary<string, string>();
            foreach (var (name, item) in textures) {
                item.PostDispatchKernel(shader, id);
            }
        }
    }

    private void ComputeSecondarySeeds() {
        var random = new System.Random(seed);
        permutationSeed.x = random.Next(-1000, 1000);
        permutationSeed.y = random.Next(-1000, 1000);
        permutationSeed.z = random.Next(-1000, 1000);
        moduloSeed.x = random.Next(-1000, 1000);
        moduloSeed.y = random.Next(-1000, 1000);
        moduloSeed.z = random.Next(-1000, 1000);
    }

    public void RandomizeSeed() {
        seed = UnityEngine.Random.Range(-9999, 9999);
        ComputeSecondarySeeds();
        OnPropertiesChanged();
    }
}