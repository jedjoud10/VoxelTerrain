using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {

    // A voxel graph is the base class to inherit from to be able to write custom voxel stuff
    public abstract partial class ManagedTerrainGraph : MonoBehaviour {
        private void OnValidate() {
            GetComponent<ManagedTerrainCompiler>().SoftRecompile();
            GetComponent<ManagedTerrainPreview>()?.OnPropertiesChanged();
        }

        public class PropContext {
            internal CustomCodeChainedNode chain;

            public PropContext() {
                chain = null;
            }

            public void TrySpawnProp(Variable<bool> shouldSpawn, Props.GenerationProp prop) {
                chain = CustomCode.WithNext(chain, (UntypedVariable self, TreeContext ctx) => {
                    Props.GenerationProp copy = prop;
                    shouldSpawn.Handle(ctx);
                    ctx.AddLine("// this is some very cool prop spawning call....");
                });
            }
        }

        public class PropInput {
            public Variable<float3> position;
            public Variable<float> density;
            public Variable<float3> normal;
        }

        public class VoxelInput {
            public Variable<float3> position;
            public Variable<int3> id;
        }

        public class VoxelOutput {
            public Variable<float> density;

            public VoxelOutput(Variable<float> density) {
                this.density = density;
            }
        }

        public abstract void Voxels(VoxelInput input, out VoxelOutput output);
        public abstract void Props(PropInput input, PropContext propContext);
    }
}