using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Props {
    /// <summary>
    /// Props of the same type are stored in the same GPU buffer, so no need to do any funky stuff with that
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BlittableProp {
        // Size in bytes of the blittable prop
        public const int size = 16;

        // in WORLD space!!!
        // you could get around spawning these in segment space but wtv for now. it works...
        public half pos_x;
        public half pos_y;
        public half pos_z;
        public half scale;

        // 4 bytes for quaternion based rotation (x,y,z,w)
        public byte rot_x;
        public byte rot_y;
        public byte rot_z;
        public byte rot_w;

        // Prop variant
        public byte variant;

        // Prop identifier (normalized dispatch index)
        // id = dispatch index - temp prop offset
        public byte id_byte1;
        public byte id_byte2;
        public byte id_byte3;

        public static BlittableProp None = new BlittableProp();
    }
}