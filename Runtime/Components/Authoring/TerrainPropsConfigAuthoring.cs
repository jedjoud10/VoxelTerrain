using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Props {
    class TerrainPropsConfigAuthoring : MonoBehaviour {
        public List<PropType> props;
        public ComputeShader copyTempToPermCompute;
    }

    class TerrainPropsConfigBaker : Baker<TerrainPropsConfigAuthoring> {
        public override void Bake(TerrainPropsConfigAuthoring authoring) {
            Entity self = GetEntity(TransformUsageFlags.None);

            // I LOVE LINQ!!!! I LOVE WRITING FUNCTIONAL CODE!!!!!!
            List<PropType.Baked> baked = authoring.props.Select(type => {
                int count = type.variants.Count;
                Entity[] prototypes = new Entity[count];
                Mesh[] meshes = new Mesh[count];
                /*
                Texture2D[] diffuse = new Texture2D[count];
                Texture2D[] normal = new Texture2D[count];
                Texture2D[] mask = new Texture2D[count];
                */

                for (int i = 0; i < count; i++) {
                    PropType.Variant variant = type.variants[i];
                    prototypes[i] = GetEntity(variant.prefab, TransformUsageFlags.Renderable);
                    meshes[i] = GetComponent<MeshFilter>(variant.prefab).sharedMesh;


                    /*
                    Material material = GetComponent<MeshRenderer>(variant.prefab).sharedMaterial;
                    diffuse[i] = Texture2D.whiteTexture;
                    if (material.HasTexture("_DiffuseMap"))
                        diffuse[i] = (Texture2D)material.GetTexture("_DiffuseMap");

                    normal[i] = Texture2D.normalTexture;
                    if (material.HasTexture("_NormalMap"))
                        normal[i] = (Texture2D)material.GetTexture("_NormalMap");

                    mask[i] = Texture2D.whiteTexture;
                    if (material.HasTexture("_MaskMap"))
                        mask[i] = (Texture2D)material.GetTexture("_MaskMap");
                    */
                }

                return new PropType.Baked {
                    prototypes = prototypes,
                    /*
                    meshes = meshes,
                    diffuseTexs = diffuse,
                    normalTexs = normal,
                    maskTexs = mask,
                    */
                };
            }).ToList();

            AddComponentObject(self, new TerrainPropsConfig {
                props = authoring.props,
                baked = baked,
                compute = authoring.copyTempToPermCompute
            });
        }
    }
}