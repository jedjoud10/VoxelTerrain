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
                Entity[] variantEntities = type.variants.Select(variant => {
                    return GetEntity(variant.prefab, TransformUsageFlags.Renderable);
                }).ToArray();

                return new PropType.Baked { variants = variantEntities };
            }).ToList();

            AddComponentObject(self, new TerrainPropsConfig {
                props = authoring.props,
                baked = baked,
                copyTempToPermCompute = authoring.copyTempToPermCompute
            });
        }
    }
}