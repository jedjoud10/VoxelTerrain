using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Props {

    // TODO: Also implement the prop type system
    // TODO: Also implement the prop variants system
    // TODO: implement dispatch index
    public struct BlittableProp {
        // Size in bytes of the blittable prop
        public const int size = 8;

        public half pos_x;
        public half pos_y;
        public half pos_z;
        public half scale;

        /*
        // 3 bytes for rotation (x,y,z)
        public byte rot_x;
        public byte rot_y;
        public byte rot_z;

        // byte used to check if the prop should spawn
        public byte spawn;
        */

        public static BlittableProp None = new BlittableProp();
    }

    // TODO: Also implement the prop type system
    // TODO: Also implement the prop variants system
    // TODO: implement dispatch index
    public struct Prop {
        public float3 position;
        public float scale;
    }
}