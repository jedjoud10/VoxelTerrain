using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Props {
    public class TerrainPropsConfig : IComponentData {
        public List<PropType> props;
        public List<PropType.Baked> baked;
        public ComputeShader copy;
        public ComputeShader cull;
        public ComputeShader apply;

    }
}