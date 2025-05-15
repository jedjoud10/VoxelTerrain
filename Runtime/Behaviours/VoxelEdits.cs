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

            List<NativeMultiCounter> counters = new List<NativeMultiCounter>();

            VoxelEditResults results = new VoxelEditResults() { counters = counters, finishedChunksCount = 0 };

            // Make a list of all the chunks that could possibly be affected by the edit (using AABB checks)
            int affected = 0;
            List<VoxelChunk> chunks = new List<VoxelChunk>();
            throw new NotImplementedException();
            /*
            foreach (var (key, chunk) in terrain.totalChunks) {
                var voxelChunk = chunk.GetComponent<VoxelChunk>();

                if (voxelChunk.GetBounds().Intersects(editBounds)) {
                    affected++;
                    chunks.Add(voxelChunk);
                }
            }
            */

            results.affectedChunks = affected;
            NativeArray<JobHandle> handles = new NativeArray<JobHandle>(affected, Allocator.Temp);

            // Start the edit jobs all at once, and they will execute in parallel to each other...
            for (int i = 0; i < chunks.Count; i++) {
                var counter = new NativeMultiCounter(VoxelUtils.MAX_MATERIAL_COUNT, Allocator.Persistent);
                counters.Add(counter);
                VoxelChunk chunk = chunks[i];

                JobHandle handle = edit.Apply(chunk.transform.position, chunk.voxels, counter);
                handles[i] = handle;
            }

            // Since we have inter-chunk dependency we must wait until ALL the edit jobs are done
            // This could be improved but works fine for now
            JobHandle.CompleteAll(handles);
            handles.Dispose();

            // Start generating the meshes after that
            foreach (var chunk in chunks) {
                terrain.mesher.GenerateMesh(chunk, immediate, (VoxelChunk chunk) => {
                    results.IncrementAndCheck(callback);
                });
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
        internal List<NativeMultiCounter> counters;
        internal int finishedChunksCount;
        internal int affectedChunks;

        public int GetCount(int materialIndex) {
            int count = 0;
            foreach (var item in counters) {
                count += item[materialIndex];
            }
            return count;
        }

        internal void IncrementAndCheck(Action<VoxelEditResults> callback) {
            finishedChunksCount++;
            if (finishedChunksCount == affectedChunks) {
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