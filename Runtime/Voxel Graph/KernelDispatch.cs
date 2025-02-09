
namespace jedjoud.VoxelTerrain.Generation {
    public class KernelOutput {
        public ScopeArgument output;
        public string outputTextureName;
    }

    public class KernelDispatch {
        public string name;
        public int depth;
        public int sizeReductionPower;
        public bool threeDimensions;
        public string scopeName;
        public int scopeIndex;
        public string numThreads;
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

                kernelOutputSetter += $"    {item.outputTextureName}_write[{input}] = {item.output.name};\n";
            }

            return $@"
#pragma kernel CS{scopeName}
{numThreads}
// Name: {name}, Scope name: {scopeName}, Scope index: {scopeIndex}, Outputs: {outputs.Length}, Arguments: {scope.arguments.Length}
void CS{scopeName}(uint3 id : SV_DispatchThreadID) {{
    uint3 remapped = uint3({remappedCoords});
    //float3 position = (float3(remapped * {frac}) + offset) * scale
    float3 position = ConvertIntoWorldPosition(float3(remapped) * {frac});
{scope.InitializeTempnation()}
{scope.Callenate()}
{kernelOutputSetter}
}}";
        }
    }
}