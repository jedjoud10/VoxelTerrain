using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using System;

namespace jedjoud.VoxelTerrain.Generation {
    public class VoxelReadback : VoxelBehaviour {
        [Range(1, 8)]
        public int asyncReadbackPerTick = 1;

        private OngoingVoxelReadback[] readbacks;
        private Queue<Vector3Int> queuedOctalUnits;
        private HashSet<Vector3Int> pendingOctalUnits;

        public delegate void OnReadbackSuccessful(VoxelChunk chunk, bool empty);
        public event OnReadbackSuccessful onReadbackSuccessful;

        // Currently ongoing async readback request
        private class OngoingVoxelReadback {
            public bool free;
            public JobHandle? pendingCopies;
            public NativeArray<uint> data;
            public List<VoxelChunk> chunks;
            public NativeArray<JobHandle> copies;

            public OngoingVoxelReadback() {
                data = new NativeArray<uint>(VoxelUtils.VOLUME * 8, Allocator.Persistent);
                chunks = new List<VoxelChunk>();
                copies = new NativeArray<JobHandle>(8, Allocator.Persistent);
                free = true;
                pendingCopies = null;
            }

            public void Reset() {
                chunks.Clear();
                free = true;
                pendingCopies = null;
                copies.AsSpan().Fill(default);
            }

            public void Dispose() {
                data.Dispose();
                copies.Dispose();
            }
        }

        public override void CallerStart() {
            pendingOctalUnits = new HashSet<Vector3Int>();
            queuedOctalUnits = new Queue<Vector3Int>();
            readbacks = new OngoingVoxelReadback[asyncReadbackPerTick];
            for (int i = 0; i < asyncReadbackPerTick; i++) {
                readbacks[i] = new OngoingVoxelReadback();
            }
        }

        // Add the given chunk inside the queue for voxel generation
        public void GenerateVoxels(VoxelChunk chunk) {
            chunk.state = VoxelChunk.ChunkState.VoxelGeneration;
            Vector3Int octalPosition = chunk.chunkPosition / 2;

            if (pendingOctalUnits.Contains(octalPosition)) return;

            queuedOctalUnits.Enqueue(octalPosition);
            pendingOctalUnits.Add(octalPosition);
        }

        // Get the latest chunk in the queue and generate voxel data for it
        public override void CallerTick() {
            for (int i = 0; i < asyncReadbackPerTick; i++) {
                OngoingVoxelReadback readback = readbacks[i];

                if (readback.pendingCopies.HasValue && readback.pendingCopies.Value.IsCompleted) {
                    readback.pendingCopies.Value.Complete();

                    // Since we're now using inter-chunk dependencies (for meshing), we can't use the pos/neg optimization, since now we need
                    // to check the neighbours values on the CPU, which goes against the idea of doing the check atomically on the GPU in the first place
                    // No pos-neg optimization for you little bro...
                    for (int j = 0; j < readback.chunks.Count; j++) {
                        VoxelChunk chunk = readback.chunks[j];
                        chunk.state = VoxelChunk.ChunkState.Temp;
                        onReadbackSuccessful?.Invoke(chunk, false);
                    }

                    readback.Reset();
                }

                if (!readback.free) {
                    continue;
                }

                if (queuedOctalUnits.TryDequeue(out var temp)) {
                    Vector3Int position = temp;
                    pendingOctalUnits.Remove(position);
                    readback.free = false;


                    // Size*2 since we are using octal generation!
                    Vector3 worldPosition = 2.0f * (Vector3)position * VoxelUtils.SIZE * terrain.voxelSizeFactor;
                    Vector3 worldScale = Vector3.one * terrain.voxelSizeFactor;
                    terrain.executor.ExecuteShader(VoxelUtils.SIZE*2, terrain.compiler.voxelsDispatchIndex, worldPosition, worldScale, true, true);

                    // Change chunk states, since we are now waiting for voxel readback
                    readback.chunks.Clear();
                    for (int j = 0; j < 8; j++) {
                        int3 temp2 = (int3)VoxelUtils.IndexToPosMorton(j);
                        Vector3Int offset = new Vector3Int(temp2.x, temp2.y, temp2.z);

                        if (terrain.totalChunks.ContainsKey(position * 2 + offset)) {
                            var chunk = terrain.totalChunks[position * 2 + offset].GetComponent<VoxelChunk>();
                            chunk.state = VoxelChunk.ChunkState.VoxelReadback;
                            readback.chunks.Add(chunk);
                        }
                    }

                    // Request GPU data into the native array we allocated at the start
                    // When we get it back, start off multiple memcpy jobs that we can wait for the next tick
                    // This avoids waiting on the memory copies and can spread them out on many threads
                    NativeArray<uint> voxelData = readback.data;
                    AsyncGPUReadback.RequestIntoNativeArray(
                        ref voxelData,
                        terrain.executor.textures["voxels"], 0,
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
                                    // Allows us to avoid generating meshes for specific chunks.
                                    VoxelChunk chunk = readback.chunks[j];

                                    // Since we are using morton encoding, an 2x2x2 unit contains 8 sequential chunks
                                    // We just need to do some memory copies at the right src offsets
                                    uint* src = pointer + (VoxelUtils.VOLUME * j);

                                    uint* dst = (uint*)chunk.voxels.GetUnsafePtr();
                                    readback.copies[j] = new UnsafeAsyncMemCpy {
                                        src = src,
                                        dst = dst,
                                    }.Schedule();
                                }

                                readback.pendingCopies = JobHandle.CombineDependencies(readback.copies.Slice(0, readback.chunks.Count));
                            }
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