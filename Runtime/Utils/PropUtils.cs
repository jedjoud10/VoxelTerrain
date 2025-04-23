using jedjoud.VoxelTerrain.Props;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    public static class PropUtils {
        public static GpuProp UnpackProp(BlittableProp prop) {
            static float Single(byte rot) {
                const float RATIO = 360.0f / 255.0f;
                return ((float)rot * RATIO);
            }

            float3 position = new half3(prop.pos_x, prop.pos_y, prop.pos_z);
            float scale = prop.scale;
            float3 rotation = new float3(Single(prop.rot_x), Single(prop.rot_y), Single(prop.rot_z));

            return new GpuProp() {
                position = position,
                rotation = rotation,
                scale = scale,
                type = 0,
                variant = prop.variant,
            };
        }
    }
}