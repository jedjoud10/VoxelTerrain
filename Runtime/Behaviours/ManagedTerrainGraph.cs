using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Generation {

    // A voxel graph is the base class to inherit from to be able to write custom voxel stuff
    public abstract partial class ManagedTerrainGraph : MonoBehaviour {
        private void OnValidate() {
            GetComponent<ManagedTerrainCompiler>().SoftRecompile();
            GetComponent<ManagedTerrainCompiler>().OnPropertiesChanged();
        }

        public class Context {
            internal CustomCodeChainedNode chain;

            public Context() {
                chain = null;
            }

            public void SpawnProp(Props.GenerationProp prop) {
                chain = CustomCode.WithNext(chain, (UntypedVariable self, TreeContext ctx) => {
                    Props.GenerationProp copy = prop;
                    ctx.AddLine("// this is some very cool prop spawning call....");
                });
            }
        }

        public class AllInputs {
            public Variable<float3> position;
            public Variable<int3> id;
        }

        public class AllOutputs {
            public Variable<float> density;
        }

        public abstract void Execute(Context context, AllInputs input, out AllOutputs output);
    }
}