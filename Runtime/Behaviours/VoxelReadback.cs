using System.Collections.Generic;
using System.Collections;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using System;

namespace jedjoud.VoxelTerrain.Generation {
    public class VoxelReadback : VoxelBehaviour {
        [Range(1, 8)]
        public int asyncReadbackPerTick = 1;

        internal List<NativeArray<uint>> voxelNativeArrays;
        internal BitArray freeVoxelNativeArrays;
        internal Queue<Vector3Int> queuedOctalUnits;
        internal HashSet<Vector3Int> pendingOctalUnits;

        public delegate void OnReadbackSuccessful(VoxelChunk chunk);
        public event OnReadbackSuccessful onReadbackSuccessful;

        public override void CallerStart() {
            //Debug.Log($"Async Compute: {SystemInfo.supportsAsyncCompute}, Async Readback: {SystemInfo.supportsAsyncGPUReadback}");
            freeVoxelNativeArrays = new BitArray(asyncReadbackPerTick, true);
            pendingOctalUnits = new HashSet<Vector3Int>();
            queuedOctalUnits = new Queue<Vector3Int>();
            voxelNativeArrays = new List<NativeArray<uint>>(asyncReadbackPerTick);
            for (int i = 0; i < asyncReadbackPerTick; i++) {
                //voxelNativeArrays.Add(new NativeArray<uint>(VoxelUtils.Volume, Allocator.Persistent));
                voxelNativeArrays.Add(new NativeArray<uint>(VoxelUtils.Volume*8, Allocator.Persistent));
            }
        }

        public override void CallerDispose() {
            AsyncGPUReadback.WaitAllRequests();
            foreach (var item in voxelNativeArrays) {
                item.Dispose();
            }
        }


        // Add the given chunk inside the queue for voxel generation
        public void GenerateVoxels(VoxelChunk chunk) {
            Vector3Int octalPosition = chunk.chunkPosition / 2;
            //Vector3Int octalPosition = chunk.chunkPosition;

            if (pendingOctalUnits.Contains(octalPosition)) return;

            queuedOctalUnits.Enqueue(octalPosition);
            pendingOctalUnits.Add(octalPosition);
        }

        /*
        [BurstCompile]
        unsafe struct FillUp : IJobParallelFor {
            [ReadOnly]
            [NativeDisableUnsafePtrRestriction]
            public half* densities;
            [WriteOnly]
            public NativeArray<Voxel> voxels;
            public void Execute(int index) {
                voxels[index] = new Voxel {
                    density = densities[index],
                    material = 0,
                };
            }
        }
        */

        [BurstCompile]
        unsafe struct FillUp : IJobParallelFor {
            [ReadOnly]
            [NativeDisableUnsafePtrRestriction]
            public uint* raw;
            [WriteOnly]
            public NativeArray<Voxel> voxels;
            public void Execute(int index) {
                voxels[index] = new Voxel {
                    density = (half)math.f16tof32(raw[index] & 0xFFFF),
                    //density = UnsafeUtility.As()
                    //density = (half)raw[index],
                    material = (byte)((raw[index] >> 16) & 0xFF),
                };
            }
        }

        // Get the latest chunk in the queue and generate voxel data for it
        public override void CallerTick() {
            for (int i = 0; i < asyncReadbackPerTick; i++) {
                if (!freeVoxelNativeArrays[i]) {
                    continue;
                }

                int cpy = i;
                NativeArray<uint> data = voxelNativeArrays[i];
                if (queuedOctalUnits.TryDequeue(out var temp)) {
                    Vector3Int position = temp;
                    pendingOctalUnits.Remove(position);
                    freeVoxelNativeArrays[i] = false;

                    Vector3 worldPosition = (Vector3)position * VoxelUtils.Size * VoxelUtils.VoxelSizeFactor;
                    terrain.executor.ExecuteShader(VoxelUtils.Size*2, 0, worldPosition * 2.0f, Vector3.one / 2, true, true);
                    AsyncGPUReadback.RequestIntoNativeArray(
                        ref data,
                        terrain.executor.textures["voxels"], 0,
                        delegate (AsyncGPUReadbackRequest asyncRequest) {
                            unsafe {
                                // We have to do this to stop unity from complaining about using the data...
                                // fuck you...
                                uint* pointer = (uint*)NativeArrayUnsafeUtility.GetUnsafePtr<uint>(data);
                                //BulkAsyncRequest bulk = new BulkAsyncRequest() { currentChunkCount = 0, bitArray = freeVoxelNativeArrays, index = cpy };

                                for (int j = 0; j < 8; j++) {
                                    // TODO: do funny silly check!!!

                                    int3 temp2 = (int3)VoxelUtils.IndexToPos(j);
                                    Vector3Int offset = new Vector3Int(temp2.x, temp2.y, temp2.z);

                                    if (terrain.totalChunks.ContainsKey(position * 2 + offset)) {
                                        var chunk = terrain.totalChunks[position * 2 + offset].GetComponent<VoxelChunk>();

                                        JobHandle handle = new FillUp() {
                                            // nghh I love unsafe pointers... 🤤👅
                                            raw = pointer + (VoxelUtils.Volume * j),
                                            voxels = chunk.voxels,
                                        }.Schedule(VoxelUtils.Volume, 8192 * VoxelUtils.SchedulingInnerloopBatchCount);
                                        handle.Complete();

                                        // FIXME: Really unsafe, since we're assuming that meshing takes less time (takes one tick or less) for all the chunks
                                        // If not, then we could theoretically be doing another async request on a texture that's still in use by chunks that have not copied their data over yet
                                        chunk.dependency = handle;
                                        onReadbackSuccessful?.Invoke(chunk);
                                    }
                                }

                                freeVoxelNativeArrays[cpy] = true;
                            }
                        }
                    );
                }
            }
        }
    }

    public class BulkAsyncRequest {
        public int currentChunkCount;
        public BitArray bitArray;
        public int index;

        public void Dispose() {
            currentChunkCount++;

            if (currentChunkCount == 8) {
                bitArray[index] = true;
            }
        }
    }
}