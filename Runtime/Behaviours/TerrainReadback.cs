using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;

namespace jedjoud.VoxelTerrain.Generation {
    public class TerrainReadback : TerrainBehaviour {
        private QueueDedupped<TerrainChunk> queue;
        
        public delegate void OnReadback(TerrainChunk chunk, bool skipped);
        public event OnReadback onReadback;

        private bool free;
        private NativeArray<uint> data;
        private List<TerrainChunk> chunks;
        private JobHandle? pendingCopies;
        private NativeArray<JobHandle> copies;
        private NativeArray<int> signCounters;
        private bool signCountersFetched, voxelsFetched;
        private MultiReadbackExecutor multiExecutor;
        private ComputeBuffer signCountersBuffer;

        public bool Free => queue.IsEmpty();
        public int InQueue => queue.Count;

        public override void CallerStart() {
            queue = new QueueDedupped<TerrainChunk>();

            data = new NativeArray<uint>(VoxelUtils.VOLUME * VoxelUtils.MULTI_READBACK_CHUNK_COUNT, Allocator.Persistent);
            chunks = new List<TerrainChunk>(VoxelUtils.MULTI_READBACK_CHUNK_COUNT);
            copies = new NativeArray<JobHandle>(VoxelUtils.MULTI_READBACK_CHUNK_COUNT, Allocator.Persistent);
            signCounters = new NativeArray<int>(VoxelUtils.MULTI_READBACK_CHUNK_COUNT, Allocator.Persistent);
            free = true;
            pendingCopies = null;
            voxelsFetched = false;
            signCountersFetched = false;

            multiExecutor = new MultiReadbackExecutor();
            signCountersBuffer = new ComputeBuffer(VoxelUtils.MULTI_READBACK_CHUNK_COUNT, sizeof(int), ComputeBufferType.Structured);
        }

        private void ResetInternally() {
            free = true;
            chunks.Clear();
            pendingCopies = null;
            copies.AsSpan().Fill(default);
            voxelsFetched = false;
            signCountersFetched = false;
        }

        public void GenerateVoxels(TerrainChunk chunk) {
            queue.Enqueue(chunk);
        }

        public override void CallerTick() {
            if (free) {
                TryBeginReadback();
            } else {
                TryCheckIfReadbackComplete();
            }
        }

        private void TryBeginReadback() {
            TerrainChunk[] array = queue.Take(VoxelUtils.MULTI_READBACK_CHUNK_COUNT);

            if (array.Length == 0) {
                return;
            }

            MultiReadbackTransform[] multiTransforms = new MultiReadbackTransform[VoxelUtils.MULTI_READBACK_CHUNK_COUNT];

            free = false;

            // Change chunk states, since we are now waiting for voxel readback
            chunks.Clear();
            chunks.AddRange(array);
            for (int j = 0; j < array.Length; j++) {
                TerrainChunk chunk = array[j];

                float3 pos = (float3)chunk.node.position;
                float scale = chunk.node.size / VoxelUtils.PHYSICAL_CHUNK_SIZE;

                multiTransforms[j] = new MultiReadbackTransform {
                    scale = scale,
                    position = pos
                };
            }

            MultiReadbackExecutorParameters parameters = new MultiReadbackExecutorParameters() {
                commandBufferName = "Terrain Readback System Async Dispatch",
                multiTransforms = multiTransforms,
                kernelName = "CSVoxels",
                updateInjected = false,
                compiler = terrain.compiler,
                seeder = terrain.seeder,
                signCountersBuffer = signCountersBuffer,
            };

            GraphicsFence fence = multiExecutor.Execute(parameters);
            CommandBuffer cmds = new CommandBuffer();
            cmds.name = "Terrain Readback System Async Readback";
            cmds.WaitOnAsyncGraphicsFence(fence, SynchronisationStageFlags.ComputeProcessing);

            // Request GPU data into the native array we allocated at the start
            // When we get it back, start off multiple memcpy jobs that we can wait for the next tick
            // This avoids waiting on the memory copies and can spread them out on many threads
            NativeArray<uint> voxelData = data;
            cmds.RequestAsyncReadbackIntoNativeArray(
                ref voxelData,
                multiExecutor.Buffers["voxels"],
                delegate (AsyncGPUReadbackRequest asyncRequest) {
                    unsafe {
                        if (disposed)
                            return;

                        // We have to do this to stop unity from complaining about using the data...
                        // fuck you...
                        uint* pointer = (uint*)NativeArrayUnsafeUtility.GetUnsafePtr<uint>(data);

                        // Start doing the memcpy asynchronously...
                        for (int j = 0; j < chunks.Count; j++) {
                            TerrainChunk chunk = chunks[j];

                            // Since we are using a buffer where the data for chunks is contiguous
                            // we can just do parallel copies from the source buffer at the appropriate offset
                            uint* src = pointer + (VoxelUtils.VOLUME * j);

                            JobHandle dep = JobHandle.CombineDependencies(chunk.asyncReadJobHandle, chunk.asyncWriteJobHandle);
                            if (!dep.IsCompleted)
                                Debug.LogWarning($"Chunk {chunk.node.position} is doing a GpuToCpuCopy whilst the previous async[Read,Write]Job is has not completed yet.");

                            JobHandle handle = new GpuToCpuCopy {
                                cpuData = chunk.voxels,
                                rawGpuData = src,
                            }.Schedule(BatchUtils.BATCH, VoxelUtils.VOLUME, dep);
  
                            copies[j] = handle;
                            chunk.asyncWriteJobHandle = handle;
                        }

                        pendingCopies = JobHandle.CombineDependencies(copies.Slice(0, chunks.Count));
                        voxelsFetched = true;
                    }
                }
            );

            cmds.RequestAsyncReadbackIntoNativeArray(
                ref signCounters,
                signCountersBuffer,
                delegate (AsyncGPUReadbackRequest asyncRequest) {
                    if (disposed)
                        return;

                    signCountersFetched = true;
                }
            );

            Graphics.ExecuteCommandBuffer(cmds);
        }

        private void TryCheckIfReadbackComplete() {
            if (pendingCopies.HasValue && pendingCopies.Value.IsCompleted && signCountersFetched && voxelsFetched) {
                pendingCopies.Value.Complete();

                // Since we now fetch n+2 voxels (34^3) we can actually use the sign optimizations
                // to check early if we need to do any meshing for a chunk whose voxels are from the GPU!
                // heheheha....
                for (int j = 0; j < chunks.Count; j++) {
                    int count = signCounters[j];
                    TerrainChunk chunk = chunks[j];

                    // Skip empty chunks!!!
                    int max = VoxelUtils.VOLUME;
                    bool empty = count == max || count == -max;
                    bool skipIfEmpty = chunk.skipIfEmpty;
                    onReadback?.Invoke(chunk, empty && skipIfEmpty);
                }

                ResetInternally();
            }
        }

        public override void CallerDispose() {
            AsyncGPUReadback.WaitAllRequests();
            data.Dispose();
            chunks.Clear();
            signCounters.Dispose();
            copies.Dispose();
            multiExecutor.DisposeResources();
            signCountersBuffer.Dispose();
        }
    }
}