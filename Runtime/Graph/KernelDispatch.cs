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

            string indent = new string(' ', 4);

            string before = InjectBeforeScopeInit(ctx);
            before = indent + before.Replace("\n", "\n" + indent);

            string after = InjectAfterScopeCalls(ctx);
            after = indent + after.Replace("\n", "\n" + indent);

            string code = $@"#pragma kernel CS{scopeName}
[numthreads({numThreads.x}, {numThreads.y}, {numThreads.z})]
// Name: {name}, Scope name: {scopeName}, Scope index: {scopeIndex}, Arguments: {scope.arguments.Length}, Nodes: {scope.nodesToNames.Count}
void CS{scopeName}(uint3 id : SV_DispatchThreadID) {{
{beginSomeSortOfGuard}
{before}
{scope.InitArgVars()}
{scope.CallWithArgs()}
{after}
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
            return @"
Voxel voxel;
voxel.density = density;
voxel.material = material;
voxel.layers = 0;
StoreVoxel(id, voxel);";
        }
    }

    public class LayerKernelDispatch : KernelDispatch {
        public override string InjectBeforeScopeInit(TreeContext ctx) {
            return @"
if (any(id >= (uint)size)) {
    return;
}

float3 position = ConvertIntoWorldPosition(id);
Voxel cachedVoxel = ReadCachedVoxel(id);
float cachedDensity = cachedVoxel.density;
float3 cachedNormal = ReadCachedNormal(id);
";
        }

        public override string InjectAfterScopeCalls(TreeContext ctx) {
            return @"
Voxel voxel = cachedVoxel;
voxel.layers = layers;
StoreVoxel(id, voxel);";
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

if (((enabled_props_flags >> type) & 1) == 0) {
    return;
}
";
        }
    }
}