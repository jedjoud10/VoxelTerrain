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
    }

    class TerrainPropsConfigBaker : Baker<TerrainPropsConfigAuthoring> {
        public override void Bake(TerrainPropsConfigAuthoring authoring) {
            Entity self = GetEntity(TransformUsageFlags.None);

            // I LOVE LINQ!!!! I LOVE WRITING FUNCTIONAL CODE!!!!!!
            List<PropType.Baked> baked = authoring.props.Select(type => {
                int count = type.variants.Count;
                Entity[] prototypes = new Entity[count];

                Texture2D[] diffuse = new Texture2D[count];
                Texture2D[] normal = new Texture2D[count];
                Texture2D[] mask = new Texture2D[count];
                
                for (int i = 0; i < count; i++) {
                    PropType.Variant variant = type.variants[i];

                    if (variant.prefab != null && type.spawnEntities) {
                        prototypes[i] = GetEntity(variant.prefab, TransformUsageFlags.Renderable);
                    } else {
                        prototypes[i] = Entity.Null;
                    }

                    if (type.renderInstances) {
                        if (type.spawnEntities) {
                            Material material = GetComponent<MeshRenderer>(variant.prefab).sharedMaterial;
                            diffuse[i] = (Texture2D)material.GetTexture("_DiffuseMap");
                            normal[i] = (Texture2D)material.GetTexture("_NormalMap");
                            mask[i] = (Texture2D)material.GetTexture("_MaskMap");
                        } else {
                            diffuse[i] = variant.diffuse;
                            normal[i] = variant.normal;
                            mask[i] = variant.mask;
                        }
                    }

                }

                return new PropType.Baked {
                    prototypes = prototypes,
                    diffuse = diffuse,
                    normal = normal,
                    mask = mask,
                };
            }).ToList();

            AddComponentObject(self, new TerrainPropsConfig {
                props = authoring.props,
                baked = baked,
                copy = authoring.copy,
                cull = authoring.cull,
                apply = authoring.apply,
            });
        }
    }
}