using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {
    // TODO: fixme. pls fixme... please... just impl some parent-child classes.. plpplsplsplspsllpslp
    public class KernelOutput {
        public string setter;
        public string outputTextureName;
        public string outputBufferName;
        public bool buffer;
    }

    public abstract class KernelDispatch {
        public string name;
        public int depth;
        public string scopeName;
        public int scopeIndex;
        public KernelOutput[] outputs;

        public virtual string InjectBeforeScopeInit(TreeContext ctx) => "";
        public virtual string InjectAfterScopeCalls(TreeContext ctx) => "";
        public string CreateKernel(TreeContext ctx) {
            TreeScope scope = ctx.scopes[scopeIndex];
            return $@"
#pragma kernel CS{scopeName}
[numthreads(8, 8, 8)]
// Name: {name}, Scope name: {scopeName}, Scope index: {scopeIndex}, Outputs: {outputs.Length}, Arguments: {scope.arguments.Length}, Nodes: {scope.namesToNodes.Count}
void CS{scopeName}(uint3 id : SV_DispatchThreadID) {{
{InjectBeforeScopeInit(ctx)}
{scope.InitArgVars()}
{scope.CallWithArgs()}
{InjectAfterScopeCalls(ctx)}
}}";
        }
    }

    public class VoxelKernelDispatch : KernelDispatch {
        public override string InjectBeforeScopeInit(TreeContext ctx) {
            return @"
if (any(id >= (uint)size)) {
    return;
}

    float3 position = ConvertIntoWorldPosition(id);
";
        }

        public override string InjectAfterScopeCalls(TreeContext ctx) {
            KernelOutput output = outputs[0];

            return $@"
#ifdef _ASYNC_READBACK_OCTAL
voxels[CalcIdIndex(id)] = {output.setter};
CheckVoxelSign(id, voxel);
#else
voxels_write[id] = {output.setter};
#endif
";
        }
    }
}