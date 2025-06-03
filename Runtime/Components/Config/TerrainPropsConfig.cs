using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Props {
    public class TerrainPropsConfig : IComponentData {
        public List<PropType> props;
        public List<Entity[]> baked;
        public ComputeShader copy;
        public ComputeShader cull;
        public ComputeShader apply;
        public Shader instancedShader;
        public Shader impostorShader;
        public Mesh quad;
    }
}