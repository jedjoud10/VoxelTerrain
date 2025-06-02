using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Generation {
    public abstract class KernelDispatch {
        public string name;
        public int depth;
        public string scopeName;
        public int scopeIndex;
        public KeywordGuards keywordGuards = null;
        public int3 numThreads;

        public virtual string InjectBeforeScopeInit(TreeContext ctx) => "";
        public virtual string InjectAfterScopeCalls(TreeContext ctx) => "";
        public virtual string CreateKernel(TreeContext ctx) {
            TreeScope scope = ctx.scopes[scopeIndex];

            string beginSomeSortOfGuard = "";
            string endSomeSortOfGuard = "";

            if (keywordGuards != null) {
                beginSomeSortOfGuard = keywordGuards.BeginGuard();
                endSomeSortOfGuard = keywordGuards.EndGuard();
            }

            string code = $@"#pragma kernel CS{scopeName}
[numthreads({numThreads.x}, {numThreads.y}, {numThreads.z})]
// Name: {name}, Scope name: {scopeName}, Scope index: {scopeIndex}, Arguments: {scope.arguments.Length}, Nodes: {scope.nodesToNames.Count}
void CS{scopeName}(uint3 id : SV_DispatchThreadID) {{
{beginSomeSortOfGuard}
{InjectBeforeScopeInit(ctx)}
{scope.InitArgVars()}
{scope.CallWithArgs()}
{InjectAfterScopeCalls(ctx)}
{endSomeSortOfGuard}
}}";


            return code;
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
            return "    StoreVoxel(id, density, material);";
        }
    }

    public class PropKernelDispatch : KernelDispatch {
        public override string InjectBeforeScopeInit(TreeContext ctx) {
            return @"
    if (id.x >= (uint)max_combined_temp_props) {
        return;
    }

    float3 position = ConvertIntoWorldPosition(id);
    int dispatch = id.x;
    int type = SearchType(dispatch);
";
        }
    }
}