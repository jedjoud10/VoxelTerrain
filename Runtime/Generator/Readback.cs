using System.Collections.Generic;
using System.Collections;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;

namespace jedjoud.VoxelTerrain.Generation {
    public partial class VoxelGenerator : VoxelBehaviour {
        // Number of simultaneous async readbacks that happen during one frame
        [Range(1, 8)]
        public int asyncReadbacks = 1;

        // List of persistently allocated native arrays
        internal List<NativeArray<half>> voxelNativeArrays;

        // Bitset containing the voxel native arrays that are free
        internal BitArray freeVoxelNativeArrays;

        // Chunks that we must generate the voxels for
        internal Queue<VoxelChunk> pendingVoxelGenerationChunks;

        public delegate void OnReadbackSuccessful(VoxelChunk chunk);
        public event OnReadbackSuccessful onReadbackSuccessful;

        private void InitializeReadbackBuffers() {
            //Debug.Log($"Async Compute: {SystemInfo.supportsAsyncCompute}, Async Readback: {SystemInfo.supportsAsyncGPUReadback}");
            freeVoxelNativeArrays = new BitArray(asyncReadbacks, true);
            pendingVoxelGenerationChunks = new Queue<VoxelChunk>();
            voxelNativeArrays = new List<NativeArray<half>>(asyncReadbacks);
            for (int i = 0; i < asyncReadbacks; i++) {
                voxelNativeArrays.Add(new NativeArray<half>(VoxelUtils.Volume, Allocator.Persistent));
            }
        }

        private void DisposeReadbackBuffers() {
            AsyncGPUReadback.WaitAllRequests();
            foreach (var nativeArrays in voxelNativeArrays) {
                nativeArrays.Dispose();
            }
        }


        // Add the given chunk inside the queue for voxel generation
        public void GenerateVoxels(VoxelChunk chunk) {
            if (pendingVoxelGenerationChunks.Contains(chunk)) return;
            pendingVoxelGenerationChunks.Enqueue(chunk);
        }

        [BurstCompile]
        struct FillUp : IJobParallelFor {
            [ReadOnly]
            public NativeArray<half> densities;
            [WriteOnly]
            public NativeArray<Voxel> voxels;
            public void Execute(int index) {
                voxels[index] = new Voxel {
                    density = densities[index],
                    material = 0,
                };
            }
        }

        // Get the latest chunk in the queue and generate voxel data for it
        public override void CallerUpdate() {
            for (int i = 0; i < asyncReadbacks; i++) {
                if (!freeVoxelNativeArrays[i]) {
                    continue;
                }

                int cpy = i;
                NativeArray<half> data = voxelNativeArrays[i];
                if (pendingVoxelGenerationChunks.TryDequeue(out VoxelChunk chunk)) {
                    freeVoxelNativeArrays[i] = false;
                    terrain.generator.ExecuteShader(VoxelUtils.Size, chunk.transform.position / VoxelUtils.VertexScaling, Vector3.one, true, true);
                    AsyncGPUReadback.RequestIntoNativeArray(
                        ref data,
                        terrain.generator.textures["voxels"], 0,
                        delegate (AsyncGPUReadbackRequest asyncRequest) {
                            NativeArray<half> temp = new NativeArray<half>(VoxelUtils.Volume, Allocator.TempJob);
                            temp.CopyFrom(data);
                            /*
                            chunk.dependency = new FillUp() {
                                densities = data,
                                voxels = chunk.voxels,
                            }.Schedule(VoxelUtils.Volume, 2048 * VoxelUtils.SchedulingInnerloopBatchCount);
                            */

                            new FillUp() {
                                densities = temp,
                                voxels = chunk.voxels,
                            }.Schedule(VoxelUtils.Volume, 2048 * VoxelUtils.SchedulingInnerloopBatchCount).Complete();

                            temp.Dispose();

                            onReadbackSuccessful?.Invoke(chunk);
                            freeVoxelNativeArrays[cpy] = true;
                        }
                    );
                }
            }
        }
    }
}