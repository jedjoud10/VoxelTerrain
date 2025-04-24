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
using System.Reflection;
using jedjoud.VoxelTerrain.Props;

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
        // Contains both the pos-neg counts and the raw octal voxel data
        private class OngoingVoxelReadback {
            public bool countsSet;
            public bool free;
            public bool dataSet;
            public JobHandle? pendingCopies;
            public NativeArray<int> counts;
            public NativeArray<uint> data;
            public List<VoxelChunk> chunks;
            public NativeArray<JobHandle> copies;
            public int[] mortonLookup;

            public OngoingVoxelReadback() {
                data = new NativeArray<uint>(VoxelUtils.VOLUME * 8, Allocator.Persistent);
                chunks = new List<VoxelChunk>();
                mortonLookup = new int[8];
                counts = new NativeArray<int>(8, Allocator.Persistent);
                copies = new NativeArray<JobHandle>(8, Allocator.Persistent);
                dataSet = false;
                countsSet = false;
                free = true;
                pendingCopies = null;
            }

            public void Reset() {
                chunks.Clear();
                dataSet = false;
                countsSet = false;
                free = true;
                pendingCopies = null;
                copies.AsSpan().Fill(default);
            }

            public void Dispose() {
                data.Dispose();
                counts.Dispose();
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

                if (readback.countsSet && readback.dataSet) {
                    if (readback.pendingCopies == null) {
                        unsafe {
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
                    } else if (readback.pendingCopies.Value.IsCompleted) {
                        readback.pendingCopies.Value.Complete();

                        // Do the smart count neg/pos check to avoid generating meshes for chunks that we know are empty
                        for (int j = 0; j < readback.chunks.Count; j++) {
                            int mortonIndex = readback.mortonLookup[j];
                            VoxelChunk chunk = readback.chunks[j];

                            // This counts the number of positive voxels - negative voxels.
                            // So the value for empty chunks is either -VOLUME or VOLUME
                            if (readback.counts[mortonIndex] == VoxelUtils.VOLUME || readback.counts[mortonIndex] == -VoxelUtils.VOLUME) {
                                chunk.state = VoxelChunk.ChunkState.Done;
                                onReadbackSuccessful?.Invoke(chunk, true);
                            } else {
                                chunk.state = VoxelChunk.ChunkState.Temp;
                                onReadbackSuccessful?.Invoke(chunk, false);
                            }
                        }

                        readback.Reset();
                    }
                    
                }

                if (!readback.free) {
                    continue;
                }

                if (queuedOctalUnits.TryDequeue(out var temp)) {
                    Vector3Int position = temp;
                    pendingOctalUnits.Remove(position);
                    readback.free = false;


                    // Size*2 since we are using octal generation!
                    Vector3 worldPosition = 2.0f * (Vector3)position * VoxelUtils.SIZE * VoxelUtils.VoxelSizeFactor;
                    Vector3 worldScale = Vector3.one * VoxelUtils.VoxelSizeFactor;
                    terrain.executor.ExecuteShader(VoxelUtils.SIZE*2, terrain.compiler.voxelsDispatchIndex, worldPosition, worldScale, true, true);

                    // Change chunk states, since we are now waiting for voxel readback
                    readback.chunks.Clear();
                    readback.mortonLookup.AsSpan().Fill(0);
                    for (int j = 0; j < 8; j++) {
                        int3 temp2 = (int3)VoxelUtils.IndexToPosMorton(j);
                        Vector3Int offset = new Vector3Int(temp2.x, temp2.y, temp2.z);

                        if (terrain.totalChunks.ContainsKey(position * 2 + offset)) {
                            var chunk = terrain.totalChunks[position * 2 + offset].GetComponent<VoxelChunk>();
                            chunk.state = VoxelChunk.ChunkState.VoxelReadback;
                            readback.mortonLookup[readback.chunks.Count] = j;
                            readback.chunks.Add(chunk);
                        }
                    }

                    NativeArray<uint> voxelData = readback.data;
                    AsyncGPUReadback.RequestIntoNativeArray(
                        ref voxelData,
                        terrain.executor.textures["voxels"], 0,
                        delegate (AsyncGPUReadbackRequest asyncRequest) {
                            readback.dataSet = true;
                        }
                    );

                    NativeArray<int> countData = readback.counts;
                    AsyncGPUReadback.RequestIntoNativeArray(
                        ref countData,
                        terrain.executor.buffers["pos_neg_counter"],
                        delegate (AsyncGPUReadbackRequest asyncRequest) {
                            readback.countsSet = true;
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