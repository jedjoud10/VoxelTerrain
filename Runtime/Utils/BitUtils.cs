using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    public static class BitUtils {
        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void DebugCheckInsideType(int index, int size) {
            if (index < 0 || index >= size) {
                throw new System.OverflowException(
                    $"Index {index} does not fit in the bit range of [0, {size})");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBitSet(int backing, int index) {
            DebugCheckInsideType(index, 32);
            return ((backing >> index) & 1) == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(ref int backing, int index, bool value) {
            DebugCheckInsideType(index, 32);

            if (value) {
                backing |= 1 << index;
            } else {
                backing &= ~(1 << index);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBitSet(byte backing, int index) {
            DebugCheckInsideType(index, 8);
            return ((backing >> index) & 1) == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(ref byte backing, int index, bool value) {
            DebugCheckInsideType(index, 8);

            if (value) {
                backing |= (byte)(1 << index);
            } else {
                backing &= (byte)(~(1 << index) & byte.MaxValue);
            }
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void DebugCheckOnlyOneBitMask(bool2 mask) {
            if (CountTrue(mask) != 1) {
                throw new System.Exception(
                    $"There must exactly be one bool set in the bool2 mask");
            }
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void DebugCheckOnlyOneBitMask(bool3 mask) {
            if (CountTrue(mask) != 1) {
                throw new System.Exception(
                    $"There must exactly be one bool set in the bool3 mask");
            }
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public static void DebugCheckOnlyOneBitMask(bool4 mask) {
            if (CountTrue(mask) != 1) {
                throw new System.Exception(
                    $"There must exactly be one bool set in the bool4 mask");
            }
        }

        public static int CountTrue(bool2 b3) {
            return math.countbits(math.bitmask(new bool4(b3, false, false)));
        }

        public static int CountTrue(bool3 b3) {
            return math.countbits(math.bitmask(new bool4(b3, false)));
        }

        public static int CountTrue(bool4 b4) {
            return math.countbits(math.bitmask(b4));
        }

        public static int FindTrueIndex(bool4 b4) {
            DebugCheckOnlyOneBitMask(b4);
            return math.tzcnt(math.bitmask(b4));
        }

        public static int FindTrueIndex(bool3 b3) {
            DebugCheckOnlyOneBitMask(b3);
            return math.tzcnt(math.bitmask(new bool4(b3, false)));
        }

        public static uint PackByteToUint(byte a, byte b, byte c, byte d) {
            return (uint)a | (uint)b << 8 | (uint)c << 16 | (uint)d << 24;
        }

        public static float4 Byte4ToFloat4(uint packed) {
            float r = (packed & 0xFF) / 255.0f;
            float g = ((packed >> 8) & 0xFF) / 255.0f;
            float b = ((packed >> 16) & 0xFF) / 255.0f;
            float a = ((packed >> 24) & 0xFF) / 255.0f;

            return new float4(r, g, b, a);
        }

        public static uint PackUInt4ToUInt(uint4 bytes) {
            return (bytes.x & 0xFFu) | ((bytes.y & 0xFFu) << 8) | ((bytes.z & 0xFFu) << 16) | ((bytes.w & 0xFFu) << 24);
        }

        public static uint PackSnorm8(float4 value) {
            float4 n = math.clamp(value * 127f, -128f, 127f);
            return PackUInt4ToUInt((uint4)math.round(n));
        }

        public static uint PackUnorm8(float4 value) {
            float4 n = math.saturate(value) * 255f;
            return PackUInt4ToUInt((uint4)math.round(n));
        }
    }
}