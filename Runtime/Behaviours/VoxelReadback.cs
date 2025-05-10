using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using System;
using jedjoud.VoxelTerrain.Octree;

namespace jedjoud.VoxelTerrain.Generation {
    public class VoxelReadback : VoxelBehaviour {
        [Range(1, 8)]
        public int asyncReadbackPerTick = 1;

        private OngoingVoxelReadback[] readbacks;
        private Queue<VoxelChunk> queued;
        private HashSet<VoxelChunk> pending;

        public bool skipEmptyChunks;
        public delegate void OnReadback(VoxelChunk chunk);
        public event OnReadback onReadback;

        // Currently ongoing async readback request
        private class OngoingVoxelReadback {
            public bool free;
            public JobHandle? pendingCopies;
            public NativeArray<uint> data;
            public List<VoxelChunk> chunks;
            public NativeArray<JobHandle> copies;
            public NativeArray<int> counters;
            public bool countersFetched, voxelsFetched;

            public OngoingVoxelReadback() {
                data = new NativeArray<uint>(VoxelUtils.VOLUME * 8, Allocator.Persistent);
                chunks = new List<VoxelChunk>();
                copies = new NativeArray<JobHandle>(8, Allocator.Persistent);
                counters = new NativeArray<int>(8, Allocator.Persistent);
                free = true;
                pendingCopies = null;
                voxelsFetched = false;
                countersFetched = false;
            }

            public void Reset() {
                chunks.Clear();
                free = true;
                pendingCopies = null;
                copies.AsSpan().Fill(default);
                voxelsFetched = false;
                countersFetched = false;
            }

            public void Dispose() {
                data.Dispose();
                counters.Dispose();
                copies.Dispose();
            }
        }

        public override void CallerStart() {
            queued = new Queue<VoxelChunk>();
            pending = new HashSet<VoxelChunk>();
            readbacks = new OngoingVoxelReadback[asyncReadbackPerTick];
            for (int i = 0; i < asyncReadbackPerTick; i++) {
                readbacks[i] = new OngoingVoxelReadback();
            }
        }

        // Add the given chunk inside the queue for voxel generation
        // Internally will try to batch this with 7 other chunks for octal generation
        public void GenerateVoxels(VoxelChunk chunk) {
            if (pending.Contains(chunk))
                return;
            
            chunk.state = VoxelChunk.ChunkState.VoxelGeneration;
            queued.Enqueue(chunk);
            pending.Add(chunk);
        }

        // Get the latest chunk in the queue and generate voxel data for it
        public override void CallerTick() {
            for (int i = 0; i < asyncReadbackPerTick; i++) {
                OngoingVoxelReadback readback = readbacks[i];

                if (readback.pendingCopies.HasValue && readback.pendingCopies.Value.IsCompleted && readback.countersFetched && readback.voxelsFetched) {
                    readback.pendingCopies.Value.Complete();
                    
                    // Since we now fetch n+2 voxels (66^3) we can actually use the pos/neg optimizations
                    // to check early if we need to do any meshing for a chunk whose voxels are from the GPU!
                    // heheheha....
                    for (int j = 0; j < readback.chunks.Count; j++) {
                        int count = readback.counters[j];
                        VoxelChunk chunk = readback.chunks[j];

                        int max = VoxelUtils.VOLUME;
                        pending.Remove(chunk);
                        if ((count == max || count == -max) && skipEmptyChunks) {
                            chunk.state = VoxelChunk.ChunkState.Done;
                            chunk.skipped = true;
                        } else {
                            chunk.state = VoxelChunk.ChunkState.Temp;
                            onReadback?.Invoke(chunk);
                        }
                    }

                    readback.Reset();
                }

                if (!readback.free) {
                    continue;
                }

                // we have chunks!!!
                // tries to batch em up, but even if we don't have 8 it'll still work
                if (queued.Count > 0) {
                    Vector4[] posScaleOctals = new Vector4[8];

                    // I hope the GPU has no issue doing async NPOT texture readback...
                    readback.free = false;

                    // Change chunk states, since we are now waiting for voxel readback
                    readback.chunks.Clear();
                    for (int j = 0; j < 8; j++) {
                        if (queued.TryDequeue(out VoxelChunk chunk)) {
                            readback.chunks.Add(chunk);
                            Vector3 pos = chunk.node.position;
                            float scale = chunk.node.size / 64f;
                            Vector4 packed = new Vector4(pos.x, pos.y, pos.z, scale);
                            posScaleOctals[j] = packed;
                            chunk.state = VoxelChunk.ChunkState.VoxelReadback;
                        }
                        
                    }

                    // Size*2 since we are using octal generation!
                    terrain.executor.ExecuteShader(VoxelUtils.SIZE * 2, terrain.compiler.voxelsDispatchIndex, Vector3.zero, Vector3.zero, posScaleOctals, true);

                    // Request GPU data into the native array we allocated at the start
                    // When we get it back, start off multiple memcpy jobs that we can wait for the next tick
                    // This avoids waiting on the memory copies and can spread them out on many threads
                    NativeArray<uint> voxelData = readback.data;
                    AsyncGPUReadback.RequestIntoNativeArray(
                        ref voxelData,
                        terrain.executor.buffers["voxels"],
                        delegate (AsyncGPUReadbackRequest asyncRequest) {
                            unsafe {
                                if (disposed) {
                                    return;
                                }

                                // We have to do this to stop unity from complaining about using the data...
                                // fuck you...
                                uint* pointer = (uint*)NativeArrayUnsafeUtility.GetUnsafePtr<uint>(readback.data);

                                // Start doing the memcpy asynchronously...
                                for (int j = 0; j < readback.chunks.Count; j++) {
                                    VoxelChunk chunk = readback.chunks[j];

                                    // Since we are using a buffer where the data for chunks is contiguous
                                    // we can just do parallel copies from the source buffer at the appropriate offset
                                    uint* src = pointer + (VoxelUtils.VOLUME * j);

                                    uint* dst = (uint*)chunk.voxels.GetUnsafePtr();
                                    readback.copies[j] = new UnsafeAsyncMemCpy {
                                        src = src,
                                        dst = dst,
                                        byteSize = Voxel.size * VoxelUtils.VOLUME,
                                    }.Schedule();
                                }
                                
                                readback.pendingCopies = JobHandle.CombineDependencies(readback.copies.Slice(0, readback.chunks.Count));
                                readback.voxelsFetched = true;
                            }
                        }
                    );

                    NativeArray<int> counters = readback.counters;
                    AsyncGPUReadback.RequestIntoNativeArray(
                        ref counters,
                        terrain.executor.negPosOctalCountersBuffer,
                        delegate (AsyncGPUReadbackRequest asyncRequest) {
                            if (disposed) {
                                return;
                            }

                            readback.countersFetched = true;
                        }
                    );
                }
            }
        }

        public override void CallerDispose() {
            AsyncGPUReadback.WaitAllRequests();
            foreach (var item in readbacks) {
                item.Dispose();
            }
        }
    }
}