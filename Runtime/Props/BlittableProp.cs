using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Props {
    // TODO: Also implement the prop type system
    // TODO: Also implement the prop variants system
    // TODO: implement dispatch index
    // Props of the same type are stored in the same GPU buffer, so no need to do any funky stuff with that
    [StructLayout(LayoutKind.Sequential)]
    public struct BlittableProp {
        // Size in bytes of the blittable prop
        public const int size = 16;

        public half pos_x;
        public half pos_y;
        public half pos_z;
        public half scale;

        // 3 bytes for rotation (x,y,z)
        public byte rot_x;
        public byte rot_y;
        public byte rot_z;

        // Prop variant type
        public byte variant;

        public uint _padding2;

        public static BlittableProp None = new BlittableProp();
    }
}