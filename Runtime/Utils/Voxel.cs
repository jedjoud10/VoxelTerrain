using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain {
    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct GpuToCpuCopy : IJobParallelFor {
        public VoxelData cpuData;

        [NativeDisableUnsafePtrRestriction]
        public GpuVoxel* rawGpuData;
        
        public void Execute(int index) {
            GpuVoxel voxel = *(rawGpuData + index);
            cpuData.densities[index] = voxel.density;
            cpuData.materials[index] = voxel.material;
            cpuData.layers[index] = BitUtils.PackByteToUint(voxel.layer1, voxel.layer2, voxel.layer3, voxel.layer4);
        }
    }

    // AoS gpu voxel data
    // Must be at least one uint wide
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuVoxel {
        // UINT1
        public half density;
        public byte material;
        public byte _padding;

        // UINT2
        public byte layer1;
        public byte layer2;
        public byte layer3;
        public byte layer4;

        /*
        // UINT3 and UINT4 (unused)
        public uint _padding2;
        public uint _padding3;
        */
        public const int size = 2 * sizeof(uint);
    }

    // Only used for editing, for ease of use
    public struct EditVoxel {
        public float density;
        public int material;
    }

    // SoA voxel data
    public struct VoxelData {
        public NativeArray<half> densities;
        public NativeArray<byte> materials;
        public NativeArray<uint> layers;


        public VoxelData(Allocator allocator) {
            densities = new NativeArray<half>(VoxelUtils.VOLUME, allocator, NativeArrayOptions.UninitializedMemory);
            materials = new NativeArray<byte>(VoxelUtils.VOLUME, allocator, NativeArrayOptions.UninitializedMemory);
            layers = new NativeArray<uint>(VoxelUtils.VOLUME, allocator, NativeArrayOptions.UninitializedMemory);
        }

        public void CopyFrom(VoxelData other) {
            densities.CopyFrom(other.densities);
            materials.CopyFrom(other.materials);
            layers.CopyFrom(other.layers);
        }

        public EditVoxel FetchEditVoxel(int index) {
            return new EditVoxel { density = densities[index], material = materials[index] };
        }

        public void StoreEditVoxels(int index, EditVoxel voxel) {
            densities[index] = (half)voxel.density;
            materials[index] = (byte)math.clamp(voxel.material, 0, 255);
        }

        public JobHandle CopyFromAsync(VoxelData other, JobHandle dep = default) {
            JobHandle a = AsyncMemCpyUtils.CopyAsync(other.densities, densities, dep);
            JobHandle b = AsyncMemCpyUtils.CopyAsync(other.materials, materials, dep);
            JobHandle c = AsyncMemCpyUtils.CopyAsync(other.layers, layers, dep);
            return JobHandle.CombineDependencies(a, b, c);
        }

        public void Dispose() {
            densities.Dispose();
            materials.Dispose();
            layers.Dispose();
        }
    }

    // SoA unsafe ptr list voxel data
    public struct UnsafePtrListVoxelData {
        public UnsafePtrList<half> densityPtrs;
        public UnsafePtrList<byte> materialPtrs;
        public UnsafePtrList<uint> layerPtrs;


        public UnsafePtrListVoxelData(Allocator allocator) {
            densityPtrs = new UnsafePtrList<half>(VoxelUtils.VOLUME, allocator, NativeArrayOptions.UninitializedMemory);
            materialPtrs = new UnsafePtrList<byte>(VoxelUtils.VOLUME, allocator, NativeArrayOptions.UninitializedMemory);
            layerPtrs = new UnsafePtrList<uint>(VoxelUtils.VOLUME, allocator, NativeArrayOptions.UninitializedMemory);
        }

        public void AddReadOnlyRangePtrs(NativeArray<VoxelData> datas) {
            unsafe {
                foreach (var data in datas) {
                    AddReadOnlyPtrs(data);
                }
            }
        }

        public void AddReadOnlyPtrs(VoxelData data) {
            unsafe {
                densityPtrs.Add(data.densities.GetUnsafeReadOnlyPtr());
                materialPtrs.Add(data.materials.GetUnsafeReadOnlyPtr());
                layerPtrs.Add(data.layers.GetUnsafeReadOnlyPtr());
            }
        }

        public void AddNullPtrs(int count) {
            for (int i = 0; i < count; i++) {
                AddNullPtrs();
            }
        }

        public void AddNullPtrs() {
            unsafe {
                densityPtrs.Add(IntPtr.Zero);
                materialPtrs.Add(IntPtr.Zero);
                layerPtrs.Add(IntPtr.Zero);
            }
        }

        public VoxelData this[int index] {
            set {
                unsafe {
                    densityPtrs[index] = (half*)value.densities.GetUnsafeReadOnlyPtr();
                    materialPtrs[index] = (byte*)value.materials.GetUnsafeReadOnlyPtr();
                    layerPtrs[index] = (uint*)value.layers.GetUnsafeReadOnlyPtr();
                }
            }
        }

        public void CopyToDataAtIndex(int ptrIndex, int srcIndex, VoxelData dst, int dstIndex) {
            unsafe {
                half* densities = densityPtrs[ptrIndex];
                byte* materials = materialPtrs[ptrIndex];
                dst.densities[dstIndex] = densities[srcIndex];
                dst.materials[dstIndex] = materials[srcIndex];
            }
        }

        public void Dispose(JobHandle handle) {
            densityPtrs.Dispose(handle);
            materialPtrs.Dispose(handle);
            layerPtrs.Dispose(handle);
        }

        public void Dispose() {
            densityPtrs.Dispose();
            materialPtrs.Dispose();
            layerPtrs.Dispose();
        }
    }
}