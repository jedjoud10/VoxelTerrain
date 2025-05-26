using System.Runtime.CompilerServices;

namespace jedjoud.VoxelTerrain {
    public static class BitUtils {
        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void DebugCheckInsideInt(int index) {
            if (index < 0 || index >= 32) {
                throw new System.OverflowException(
                    $"Index {index} does not fit in the bit range of [0, 32)");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBitSet(int backing, int index) {
            DebugCheckInsideInt(index);
            return ((backing >> index) & 1) == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(ref int backing, int index, bool value) {
            DebugCheckInsideInt(index);

            if (value) {
                backing |= 1 << index;
            } else {
                backing &= ~(1 << index);
            }
        }
    }
}