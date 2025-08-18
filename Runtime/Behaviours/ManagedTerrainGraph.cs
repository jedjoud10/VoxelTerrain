using System;
using System.Collections.Generic;
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
            private Variable<int> dispatchIndex;
            private HashSet<int> defined;

            internal PropContext(Variable<int> dispatchIndex) {
                this.chain = null;
                this.defined = new HashSet<int>();
                this.dispatchIndex = dispatchIndex;
            }

            public void DeclarePropSpawn(int type, Variable<bool> shouldSpawn, Props.GenerationProp prop) {
                if (prop.position == null)
                    throw new NullReferenceException("Prop position cannot be null");
                if (prop.rotation == null)
                    throw new NullReferenceException("Prop rotation cannot be null");
                if (prop.variant == null)
                    throw new NullReferenceException("Prop variant cannot be null");
                if (prop.scale == null)
                    throw new NullReferenceException("Prop scale cannot be null");

                if (defined.Contains(type))
                    throw new InvalidOperationException($"Spawn condition for type '{type}' has already been declared. Only one declaration is allowed per type.");

                chain = CustomCode.WithNext(chain, (UntypedVariable self, TreeContext ctx) => {
                    prop.position.Handle(ctx);
                    prop.scale.Handle(ctx);
                    prop.rotation.Handle(ctx);
                    prop.variant.Handle(ctx);
                    shouldSpawn.Handle(ctx);
                    ctx.AddLine("// this is some very cool prop spawning call....");
                    ctx.AddLine($"ConditionalSpawnPropOfType({ctx[shouldSpawn]}, type, {type}, {ctx[prop.position]}, {ctx[prop.scale]}, {ctx[prop.rotation]}, {ctx[prop.variant]}, {ctx[dispatchIndex]});");                    
                });

                defined.Add(type);
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
                Variable<bool> hit = Variable<bool>.NonConst(false);
                Variable<float3> hitPosition = Variable<float3>.NonConst(float3.zero);
                Variable<float3> hitNormal = Variable<float3>.NonConst(float3.zero);

                chain = CustomCode.WithNext(chain, (UntypedVariable self, TreeContext ctx) => {
                    position.Handle(ctx);
                    hit.Handle(ctx);
                    hitPosition.Handle(ctx);
                    hitNormal.Handle(ctx);
                    ctx.AddLine("// this is some *extremely* cool possible surface check...");
                    ctx.AddLine($"{ctx[hit]} = CheckSurfaceAlongAxis({ctx[position]}, {_axis}, {ctx[hitPosition]}, {ctx[hitNormal]});");
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

        public abstract void Density(in Variable<float3> position, out Variable<float> density);
        public abstract void Layers(in Variable<float3> position, in Variable<float3> normal, in Variable<float> density, out Variable<float4> layers);
        public abstract void Props(in PropInput input, PropContext propContext);
    }
}