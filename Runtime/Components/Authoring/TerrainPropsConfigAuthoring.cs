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
            List<TerrainPropsConfig.BakedPropVariant[]> baked = authoring.props.Where(x => {
                if (x == null) {
                    UnityEngine.Debug.LogWarning("Prop type in TerrainPropsConfigAuthoring is null. Probably not what you want");
                    return false;
                } else {
                    return true;
                }
            }).Select(type => {
                int count = type.variants.Count;
                TerrainPropsConfig.BakedPropVariant[] baked = new TerrainPropsConfig.BakedPropVariant[count];

                for (int i = 0; i < count; i++) {
                    baked[i] = new TerrainPropsConfig.BakedPropVariant();
                    PropType.Variant variant = type.variants[i];

                    if (variant.prefab == null)
                        throw new NullReferenceException($"Type '{type.name}' at variant {i} is missing prefab (always needed, even for instanced rendering or impostors)");

                    baked[i].prototype = GetEntity(variant.prefab, TransformUsageFlags.Renderable);

                    MeshRenderer renderer = GetComponent<MeshRenderer>(variant.prefab);
                    if (renderer == null)
                        throw new NullReferenceException($"Type '{type.name}' at variant {i} is missing mesh renderer");

                    Material material = renderer.sharedMaterial;

                    if (material == null)
                        throw new NullReferenceException($"Type '{type.name}' at variant {i} is missing main material");

                    if (!material.HasTexture("_DiffuseMap"))
                        throw new NullReferenceException($"Missing _DiffuseMap at material from type '{type.name}' at variant {i}");
                    baked[i].diffuse = (Texture2D)material.GetTexture("_DiffuseMap");

                    if (!material.HasTexture("_NormalMap"))
                        throw new NullReferenceException($"Missing _NormalMap at material from type '{type.name}' at variant {i}");
                    baked[i].normal = (Texture2D)material.GetTexture("_NormalMap");

                    if (!material.HasTexture("_MaskMap"))
                        throw new NullReferenceException($"Missing _MaskMap at material from type '{type.name}' at variant {i}");
                    baked[i].mask = (Texture2D)material.GetTexture("_MaskMap");
                }

                return baked;
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