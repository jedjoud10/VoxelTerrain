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
    }
}