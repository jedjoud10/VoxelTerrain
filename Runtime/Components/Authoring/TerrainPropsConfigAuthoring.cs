using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Props {
    class TerrainPropsConfigAuthoring : MonoBehaviour {
        public List<PropType> propTypes;
        public ComputeShader copy;
        public ComputeShader cull;
        public ComputeShader apply;
        public Shader instancedShader;
        public Shader impostorShader;
        public EnabledTerrainPropsFlags enabledPropTypesFlag = (EnabledTerrainPropsFlags)int.MinValue;
    }

    class TerrainPropsConfigBaker : Baker<TerrainPropsConfigAuthoring> {
        public override void Bake(TerrainPropsConfigAuthoring authoring) {


            Entity self = GetEntity(TransformUsageFlags.None);

            // I LOVE LINQ!!!! I LOVE WRITING FUNCTIONAL CODE!!!!!!
            List<TerrainPropsConfig.BakedPropVariant[]> baked = authoring.propTypes.Where(x => {
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
                    GameObject variant = type.variants[i];

                    if (variant == null)
                        throw new NullReferenceException($"Type '{type.name}' at variant {i} is missing prefab (always needed, even for instanced rendering or impostors)");

                    baked[i].prototype = GetEntity(variant, TransformUsageFlags.Renderable);

                    Material material;
                    if (type.overrideMaterial == null) {
                        MeshRenderer renderer = GetComponent<MeshRenderer>(variant);
                        if (renderer == null)
                            throw new NullReferenceException($"Type '{type.name}' at variant {i} is missing mesh renderer");

                        if (renderer.sharedMaterial == null)
                            throw new NullReferenceException($"Type '{type.name}' at variant {i} is missing main material");
                        material = renderer.sharedMaterial;
                    } else {
                        material = type.overrideMaterial;
                    }



                    Texture2D GetMap(string map) {
                        if (material.HasTexture(map)) {
                            return (Texture2D)material.GetTexture(map);
                        } else {
                            Debug.LogWarning($"Missing {map} at material from type '{type.name}' at variant {i}");
                            return null;
                        }
                    }

                    baked[i].diffuse = GetMap("_DiffuseMap");
                    baked[i].normal = GetMap("_NormalMap");
                    baked[i].mask = GetMap("_MaskMap");
                }

                return baked;
            }).ToList();

            AddComponentObject(self, new TerrainPropsConfig {
                props = authoring.propTypes,
                baked = baked,
                copy = authoring.copy,
                cull = authoring.cull,
                apply = authoring.apply,
                instancedShader = authoring.instancedShader,
                impostorShader = authoring.impostorShader,
                enabledPropTypesFlag = authoring.enabledPropTypesFlag,
            });
        }
    }
}