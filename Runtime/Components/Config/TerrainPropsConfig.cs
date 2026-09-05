using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Props {
    public class TerrainPropsConfig : IComponentData {
        public class BakedPropVariant {
            public Entity prototype;
            public Texture2D diffuse = null;
            public Texture2D normal = null;
            public Texture2D mask = null;
        }

        public List<PropType> props;
        public List<BakedPropVariant[]> baked;
        public ComputeShader copy;
        public ComputeShader cull;
        public ComputeShader apply;
        public Shader instancedShader;
        public Shader impostorShader;
        public EnabledTerrainPropsFlags enabledPropTypesFlag;

        // unity ECS baking is VERY scuffed
        // we need to have this here otherwise the `prototype` entity of `BakedPropVariant` does not get baked properly. wtf?
        // seems like they only run the "validate baked enttiy" pass on components that have top-level properties that have entities. maybe we should stope using `BakedPropVariant` alltogether and flatten the entire thing to an array
        // I really hope this is documented behaviour (didn't look very hard)
        public List<Entity> unused;
    }
}