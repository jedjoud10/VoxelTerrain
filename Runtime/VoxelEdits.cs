using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using Unity.Collections;

namespace jedjoud.VoxelTerrain.Edits {
    public class VoxelEdits : VoxelBehaviour {
        public float expandAmount = 5.0f;
        /*
        [Range(1, 8)]
        public int editJobsPerFrame = 1;
        public delegate void OnVoxelEditApplied(VoxelChunk chunk, VoxelMesh mesh);
        public event OnVoxelEditApplied onVoxelMeshingComplete;
        internal Queue<PendingMeshJob> pendingMeshJobs;
        */

        // Apply a voxel edit to the terrain world
        // Could either be used in game (for destructible terrain) or in editor for creating the terrain map
        public void ApplyVoxelEdit(IVoxelEdit edit, bool immediate, Action<VoxelEditResults> callback = null) {
            Bounds editBounds = edit.GetBounds();
            editBounds.Expand(expandAmount);

            List<Unsafe.NativeMultiCounter> counters = new List<Unsafe.NativeMultiCounter>();

            VoxelEditResults results = new VoxelEditResults() { counters = counters, finishedChunksCount = 0 };

            int affected = 0;
            foreach (var chunk in terrain.totalChunks) {
                var voxelChunk = chunk.GetComponent<VoxelChunk>();

                if (voxelChunk.GetBounds().Intersects(editBounds)) {
                    if (voxelChunk.dependency.HasValue) {
                        voxelChunk.dependency.Value.Complete();
                    }

                    // unrelated...
                    affected++;
                }
            }

            foreach (var chunk in terrain.totalChunks) {
                var voxelChunk = chunk.GetComponent<VoxelChunk>();

                if (voxelChunk.GetBounds().Intersects(editBounds)) {
                    var counter = new Unsafe.NativeMultiCounter(VoxelUtils.MAX_MATERIAL_COUNT, Allocator.Persistent);
                    counters.Add(counter);

                    voxelChunk.dependency = edit.Apply(voxelChunk.transform.position, voxelChunk.voxels, counter);

                    terrain.mesher.GenerateMesh(voxelChunk, immediate, (VoxelChunk chunk) => {
                        results.IncrementAndCheck(affected, callback);
                    });
                }
            }
        }

        public override void CallerStart() {
        }

        public override void CallerTick() {
        }

        public override void CallerDispose() {
        }
    }

    public class VoxelEditResults {
        internal List<Unsafe.NativeMultiCounter> counters;
        internal int finishedChunksCount;

        public int GetCount(int materialIndex) {
            int count = 0;
            foreach (var item in counters) {
                count += item[materialIndex];
            }
            return count;
        }

        internal void IncrementAndCheck(int affected, Action<VoxelEditResults> callback) {
            finishedChunksCount++;
            if (finishedChunksCount == affected) {
                callback?.Invoke(this);
                Dispose();
            }
        }

        internal void Dispose() {
            foreach (var counter in counters) {
                counter.Dispose();
            }
        }
    }
}