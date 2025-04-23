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
            freeVoxelNativeArrays = new BitArray(asyncReadbackPerTick, true);
            pendingOctalUnits = new HashSet<Vector3Int>();
            queuedOctalUnits = new Queue<Vector3Int>();
            voxelNativeArrays = new List<NativeArray<uint>>(asyncReadbackPerTick);
            for (int i = 0; i < asyncReadbackPerTick; i++) {
                voxelNativeArrays.Add(new NativeArray<uint>(VoxelUtils.VOLUME*8, Allocator.Persistent));
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
                if (!freeVoxelNativeArrays[i]) {
                    continue;
                }

                int cpy = i;
                NativeArray<uint> data = voxelNativeArrays[i];
                if (queuedOctalUnits.TryDequeue(out var temp)) {
                    Vector3Int position = temp;
                    pendingOctalUnits.Remove(position);
                    freeVoxelNativeArrays[i] = false;


                    // Size*2 since we are using octal generation!
                    Vector3 worldPosition = 2.0f * (Vector3)position * VoxelUtils.SIZE * VoxelUtils.VoxelSizeFactor;
                    Vector3 worldScale = Vector3.one * VoxelUtils.VoxelSizeFactor;
                    terrain.executor.ExecuteShader(VoxelUtils.SIZE*2, terrain.graph.voxelsDispatchIndex, worldPosition, worldScale, true, true);

                    // Change chunk states, since we are now waiting for voxel readback
                    for (int j = 0; j < 8; j++) {
                        int3 temp2 = (int3)VoxelUtils.IndexToPosMorton(j);
                        Vector3Int offset = new Vector3Int(temp2.x, temp2.y, temp2.z);

                        if (terrain.totalChunks.ContainsKey(position * 2 + offset)) {
                            var chunk = terrain.totalChunks[position * 2 + offset].GetComponent<VoxelChunk>();
                            chunk.state = VoxelChunk.ChunkState.VoxelReadback;
                        }
                    }

                    AsyncGPUReadback.RequestIntoNativeArray(
                        ref data,
                        terrain.executor.textures["voxels"], 0,
                        delegate (AsyncGPUReadbackRequest asyncRequest) {
                            unsafe {
                                // We have to do this to stop unity from complaining about using the data...
                                // fuck you...
                                uint* pointer = (uint*)NativeArrayUnsafeUtility.GetUnsafePtr<uint>(data);

                                for (int j = 0; j < 8; j++) {
                                    // TODO: do the smart count neg/pos check here when we implement it
                                    // Allows us to avoid generating meshes for specific chunks.

                                    int3 temp2 = (int3)VoxelUtils.IndexToPosMorton(j);
                                    Vector3Int offset = new Vector3Int(temp2.x, temp2.y, temp2.z);

                                    if (terrain.totalChunks.ContainsKey(position * 2 + offset)) {
                                        var chunk = terrain.totalChunks[position * 2 + offset].GetComponent<VoxelChunk>();

                                        // Since we are using morton encoding, an 2x2x2 unit contains 8 sequential chunks
                                        // We just need to do some memory copies at the right src offsets
                                        uint* src = pointer + (VoxelUtils.VOLUME * j);
                                        uint* dst = (uint*)chunk.voxels.GetUnsafePtr();
                                        UnsafeUtility.MemCpy(dst, src, VoxelUtils.VOLUME * Voxel.size);

                                        chunk.state = VoxelChunk.ChunkState.Temp;
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

        public override void CallerDispose() {
            AsyncGPUReadback.WaitAllRequests();
            foreach (var item in voxelNativeArrays) {
                item.Dispose();
            }
        }
    }
}