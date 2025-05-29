using jedjoud.VoxelTerrain.Props;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public static class PropUtils {
        // we need to make this work with 10 million props otherwise I am a bad coder
        public const int MAX_PROPS_EVER = 10_000_000;

        // size of the temp buffers
        public const int MAX_PROPS_PER_SEGMENT = 32*32*32*2;
        public const int MAX_PROP_TYPES = 1;
        
        public static void UnpackProp(BlittableProp prop, out float3 position, out float scale) {
            position = new half3(prop.pos_x, prop.pos_y, prop.pos_z);
            scale = prop.scale;
            //rotation = new float3(Single(prop.rot_x), Single(prop.rot_y), Single(prop.rot_z));
        }

        public static float UnpackFixedByte(byte rot) {
            const float RATIO = 360.0f / 255.0f;
            return ((float)rot * RATIO);
        }
    }
}