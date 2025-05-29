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

            public void SpawnProp(Variable<bool> shouldSpawn, Props.GenerationProp prop) {
                chain = CustomCode.WithNext(chain, (UntypedVariable self, TreeContext ctx) => {
                    Props.GenerationProp copy = prop;
                    copy.position.Handle(ctx);
                    copy.scale.Handle(ctx);
                    shouldSpawn.Handle(ctx);
                    ctx.AddLine($@"
// this is some very cool prop spawning call....
if ({ctx[shouldSpawn]}) {{
    int index = 0;
    InterlockedAdd(temp_counters_buffer[{prop.type}], 1, index);
    index += temp_buffer_offsets_buffer[{prop.type}];
    temp_buffer[index] = PackPositionAndScale({ctx[prop.position]}, {ctx[prop.scale]});
}}
");
                    
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