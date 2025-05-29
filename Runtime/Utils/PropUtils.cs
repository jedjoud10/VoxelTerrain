using jedjoud.VoxelTerrain.Props;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public static class PropUtils {        
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