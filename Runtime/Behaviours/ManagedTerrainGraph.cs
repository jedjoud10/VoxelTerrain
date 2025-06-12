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

            public void SpawnProp(int type, Variable<bool> shouldSpawn, Props.GenerationProp prop) {
                if (prop.position == null || prop.rotation == null || prop.variant == null || prop.scale == null)
                    throw new System.NullReferenceException("One of the prop variables is null");

                chain = CustomCode.WithNext(chain, (UntypedVariable self, TreeContext ctx) => {
                    prop.position.Handle(ctx);
                    prop.scale.Handle(ctx);
                    prop.rotation.Handle(ctx);
                    prop.variant.Handle(ctx);
                    shouldSpawn.Handle(ctx);
                    ctx.AddLine("// this is some very cool prop spawning call....");
                    ctx.AddLine($"ConditionalSpawnPropOfType({ctx[shouldSpawn]}, type, {type}, {ctx[prop.position]}, {ctx[prop.scale]}, {ctx[prop.rotation]}, {ctx[prop.variant]});");                    
                });
            }

            public struct PossibleSurface {
                public Variable<bool> hit;
                public Variable<float3> hitPosition;
                public Variable<float3> hitNormal;
            }

            public enum Axis: int {
                X = 0,
                Y = 1,
                Z = 2
            }

            public PossibleSurface IsSurfaceAlongAxis(Variable<float3> position, Axis axis) {
                int _axis = (int)axis;
                Variable<bool> hit = Variable<bool>.New(false);
                Variable<float3> hitPosition = Variable<float3>.New(float3.zero);
                Variable<float3> hitNormal = Variable<float3>.New(float3.zero);

                chain = CustomCode.WithNext(chain, (UntypedVariable self, TreeContext ctx) => {
                    position.Handle(ctx);
                    hit.Handle(ctx);
                    hitPosition.Handle(ctx);
                    hitNormal.Handle(ctx);
                    ctx.AddLine($@"
// this is some *extremely* cool possible surface check...
{ctx[hit]} = CheckSurfaceAlongAxis({ctx[position]}, {_axis}, {ctx[hitPosition]}, {ctx[hitNormal]});
");
                });

                return new PossibleSurface {
                    hit = hit,
                    hitPosition = hitPosition,
                    hitNormal = hitNormal
                };
            }
        }

        public class PropInput {
            public Variable<float3> position;
            public Variable<float> density;
            public Variable<int> dispatch;
            public Variable<int> type;
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

        public class LayersInput {
            public Variable<float> density;
            public Variable<float3> normal;
        }

        public class LayersOutput {
            public Variable<float4> layers;

            public LayersOutput(Variable<float4> layers) {
                this.layers = layers;
            }
        }

        public abstract void Voxels(VoxelInput input, out VoxelOutput output);
        public abstract void Props(PropInput input, PropContext propContext);
        //public abstract void Layers(LayersInput input, out LayersOutput);
    }
}