using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace jedjoud.VoxelTerrain {
    public static class NativeExtensions {
        // https://discussions.unity.com/t/cant-use-nativebitarray-asnativearray-assertion-failed-on-expression-issecondaryversion-handle/1488470/3
        // unity can't stop hugging L's. dawg... 
        // yet another bug
        public static NativeArray<T> AsNativeArrayExt<T>(this NativeBitArray self) where T : unmanaged {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle handle = NativeBitArrayUnsafeUtility.GetAtomicSafetyHandle(self);
#endif
            var arr = self.AsNativeArray<T>();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeBitArrayUnsafeUtility.SetAtomicSafetyHandle(ref self, handle);
#endif
            return arr;
        }
    }
}