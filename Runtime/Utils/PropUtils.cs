using jedjoud.VoxelTerrain.Props;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public static class PropUtils {

        public static void UnpackProp(BlittableProp prop, out float3 position, out float scale, out quaternion rotation, out byte variant) {
            position = new half3(prop.pos_x, prop.pos_y, prop.pos_z);
            scale = prop.scale;

            // quater onion
            uint4 quaterOnion = new uint4(prop.rot_x, prop.rot_y, prop.rot_z, prop.rot_w);
            float4 quaterOnionUnpacked = ((float4)quaterOnion / 255.0f) * 2f - 1f;
            rotation = new quaternion { value = quaterOnionUnpacked };
            
            variant = prop.variant;
        }
    }
}