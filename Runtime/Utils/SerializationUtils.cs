using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    [BurstCompile]
    public struct SerializationWriter {
        private NativeList<byte> data;
        public int Written => data.Length;
        public NativeList<byte> Data => data;

        public SerializationWriter(Allocator allocator) {
            data = new NativeList<byte>(allocator);
        }

        public void WriteArray<T>(NativeArray<T> array) where T: unmanaged {
            int sizeOf = UnsafeUtility.SizeOf<T>();
            NativeArray<byte> castedSrc = array.Reinterpret<byte>(sizeOf);
            data.AddRange(castedSrc);
        }

        public void WriteByte(byte value) {
            data.Add(value);
        }

        public void WriteUint(uint value) {
            data.Add((byte)(value & 0xFF));
            data.Add((byte)((value >> 8) & 0xFF));
            data.Add((byte)((value >> 16) & 0xFF));
            data.Add((byte)((value >> 24) & 0xFF));
        }

        public void WriteInt(int value) {
            WriteUint((uint)value);
        }

        public void WriteFloat(float value) {
            WriteUint(math.asuint(value));
        }

        public void WriteFloat3(float3 value) {
            WriteFloat(value.x);
            WriteFloat(value.y);
            WriteFloat(value.z);
        }

        public void WriteInt3(int3 value) {
            WriteInt(value.x);
            WriteInt(value.y);
            WriteInt(value.z);
        }

        public void WriteQuaternion(quaternion rot) {
            WriteFloat(rot.value.x);
            WriteFloat(rot.value.y);
            WriteFloat(rot.value.z);
            WriteFloat(rot.value.w);
        }

        public void WriteFixedString32Bytes(FixedString32Bytes value) {
            WriteByte((byte)value.Length);
            for (int i = 0; i < value.Length; i++) {
                WriteByte(value[i]);
            }
        }

        public void WriteBool(bool value) {
            data.Add((byte)(value ? 1 : 0));
        }

        public void Dispose() {
            data.Dispose();
        }
    }
}