using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    // TODO: fixme. pls fixme... please... just impl some parent-child classes.. plpplsplsplspsllpslp
    public class KernelOutput {
        public string setter;
        public string outputTextureName;
        public string outputBufferName;
        public bool buffer;
    }

    public class KernelDispatch {
        public string name;
        public int depth;
        public int sizeReductionPower;
        public bool threeDimensions;
        public string scopeName;
        public int scopeIndex;
        public Vector3Int numThreads;
        public string remappedCoords;
        public bool mortonate;
        public string writeCoords;
        public KernelOutput[] outputs;

        public float frac;

        public string ConvertToKernelString(TreeContext ctx) {
            TreeScope scope = ctx.scopes[scopeIndex];

            // Create the variable definitions and assignment for variables to set within the scope
            string kernelOutputSetter = "";
            foreach (var item in outputs) {
                string input = $"id.{writeCoords}";

                if (mortonate) {
                    input = $"morton ? indexToPos(encodeMorton32({input})).xzy : {input}";
                }

                if (item.buffer) {
                    // TODO: what the fuck?????
                    kernelOutputSetter += $@"
    if ({item.setter}.scale > 0.0) {{
        int index = 0;
        InterlockedAdd({item.outputBufferName}_counter[0], 1, index);
        {item.outputBufferName}[index] = PackProp({item.setter});
    }}
";

                } else {
                    kernelOutputSetter += $"    {item.outputTextureName}_write[{input}] = {item.setter};\n";
                }
            }

            return $@"
#pragma kernel CS{scopeName}
[numthreads({numThreads.x}, {numThreads.y}, {numThreads.z})]
// Name: {name}, Scope name: {scopeName}, Scope index: {scopeIndex}, Outputs: {outputs.Length}, Arguments: {scope.arguments.Length}
void CS{scopeName}(uint3 id : SV_DispatchThreadID) {{
    uint3 remapped = uint3({remappedCoords});
    float3 position = ConvertIntoWorldPosition(float3(remapped) * {frac});
{scope.InitArgVars()}
{scope.CallWithArgs()}
{kernelOutputSetter}
}}";
        }
    }
}