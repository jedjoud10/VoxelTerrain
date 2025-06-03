using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Props {
    class TerrainPropsConfigAuthoring : MonoBehaviour {
        public List<PropType> props;
        public ComputeShader copy;
        public ComputeShader cull;
        public ComputeShader apply;
        public Shader instancedShader;
        public Shader impostorShader;
    }

    class TerrainPropsConfigBaker : Baker<TerrainPropsConfigAuthoring> {
        public override void Bake(TerrainPropsConfigAuthoring authoring) {
            Entity self = GetEntity(TransformUsageFlags.None);

            // I LOVE LINQ!!!! I LOVE WRITING FUNCTIONAL CODE!!!!!!
            List<Entity[]> baked = authoring.props.Select(type => {
                int count = type.variants.Count;
                Entity[] prototypes = new Entity[count];

                for (int i = 0; i < count; i++) {
                    PropType.Variant variant = type.variants[i];

                    if (type.spawnEntities) {
                        if (variant.prefab == null)
                            throw new NullReferenceException("Variant missing prefab!!!");

                        prototypes[i] = GetEntity(variant.prefab, TransformUsageFlags.Renderable);
                    } else {
                        prototypes[i] = Entity.Null;
                    }
                }

                return prototypes;
            }).ToList();

            AddComponentObject(self, new TerrainPropsConfig {
                props = authoring.props,
                baked = baked,
                copy = authoring.copy,
                cull = authoring.cull,
                apply = authoring.apply,
                instancedShader = authoring.instancedShader,
                impostorShader = authoring.impostorShader,
            });
        }
    }
}