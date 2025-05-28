namespace jedjoud.VoxelTerrain.Generation {
    public abstract class KernelDispatch {
        public string name;
        public int depth;
        public string scopeName;
        public int scopeIndex;

        public virtual string InjectBeforeScopeInit(TreeContext ctx) => "";
        public virtual string InjectAfterScopeCalls(TreeContext ctx) => "";
        public virtual string CreateKernel(TreeContext ctx) {
            TreeScope scope = ctx.scopes[scopeIndex];
            return $@"
#pragma kernel CS{scopeName}
[numthreads(8, 8, 8)]
// Name: {name}, Scope name: {scopeName}, Scope index: {scopeIndex}, Arguments: {scope.arguments.Length}, Nodes: {scope.namesToNodes.Count}
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
            return "    StoreVoxel(id, voxel, material);";
        }
    }

    public class PropKernelDispatch : KernelDispatch {
        public override string InjectBeforeScopeInit(TreeContext ctx) {
            return @"
    if (any(id >= (uint)size)) {
        return;
    }
    
    float3 position = ConvertIntoWorldPosition(id);
";
        }

        public override string InjectAfterScopeCalls(TreeContext ctx) {
            return "";
        }
    }
}