using System.Collections.Generic;
using Unity.Entities;

namespace jedjoud.VoxelTerrain.Props {
    public class TerrainPropsConfig : IComponentData {
        public List<PropType> props;
        public List<PropType.Baked> baked;
    }
}