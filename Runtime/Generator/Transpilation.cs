using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

public partial class VoxelGenerator : VoxelBehaviour {
    private TreeContext ctx;

    [SerializeField]
    private int hash;

    // Parses the voxel graph into a tree context with all required nodes and everything!!!
    private void ParsedTranspilation() {
        Debug.Log("Parsed Transpilation...");
        ctx = new TreeContext(debugName);
        ctx.Hash(debugName);

        // Create the external inputs that we use inside the function scope
        Variable<float3> position = ctx.AliasExternalInput<float3>("position");

        // Input scope arguments
        ctx.position = new ScopeArgument("position", GraphUtils.StrictType.Float3, position, false);

        // Execute the voxel graph to get density and color
        AllInputs inputs = new AllInputs() { position = position };
        ExecuteWithEverything(inputs, out AllOutputs outputs);

        // Voxel function output arguments 
        ScopeArgument voxelArgument = new ScopeArgument("voxel", GraphUtils.StrictType.Float, outputs.density, true);
        ScopeArgument colorArgument = new ScopeArgument("color", GraphUtils.StrictType.Float3, outputs.color, true);

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
            mortonate = true,
            numThreads = "[numthreads(8, 8, 8)]",
            remappedCoords = "id.xyz",
            writeCoords = "xyz",
            outputs = new KernelOutput[] {
                new KernelOutput { output = voxelArgument, outputTextureName = "voxels" },
                new KernelOutput { output = colorArgument, outputTextureName = "colors" },
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
    private string Transpile() {
        Debug.Log("Transpile...");

        if (ctx == null) {
            ParsedTranspilation();
        } else {
            Debug.Log("Context already set!");
        }

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
}