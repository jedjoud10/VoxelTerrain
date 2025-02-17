using jedjoud.VoxelTerrain.Props;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    public static class PropUtils {
        public static Prop UnpackProp(BlittableProp prop) {
            float3 position = new half3(prop.pos_x, prop.pos_y, prop.pos_z);
            float scale = prop.scale;

            return new Prop() {
                position = position,
                scale = scale,
            };
        }

        public static Vector3 UncompressPropRotation(ref BlittableProp prop) {
            return Vector3.zero;
            //return UncompressPropRotationFromRaw(prop.rot_x, prop.rot_y, prop.rot_z);
        }

        public static Vector3 UncompressPropRotationFromRaw(byte xRot, byte yRot, byte zRot) {
            static float Single(byte rot) {
                const float RATIO = 360.0f / 255.0f;
                return ((float)rot * RATIO);
            }

            return new Vector3(Single(xRot), Single(yRot), Single(zRot));
        }
    }
}