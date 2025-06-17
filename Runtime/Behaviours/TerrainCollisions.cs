using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Meshing {
    public class TerrainCollisions : TerrainBehaviour {
        internal List<(JobHandle, TerrainChunk)> bakeJobs;

        public override void CallerStart() {
            bakeJobs = new List<(JobHandle, TerrainChunk)>();
        }

        public void GenerateCollisions(TerrainChunk chunk) {
            CollisionBakeJob bakeJob = new CollisionBakeJob {
                meshId = chunk.sharedMesh.GetInstanceID(),
            };

            var handle = bakeJob.Schedule();
            bakeJobs.Add((handle, chunk));
        }

        public override void CallerTick() {
            for (int i = bakeJobs.Count - 1; i >= 0; i--) {
                var (handle, chunk) = bakeJobs[i];

                if (handle.IsCompleted) {
                    handle.Complete();
                    MeshCollider collider = chunk.GetComponent<MeshCollider>();
                    collider.sharedMesh = chunk.sharedMesh;
                    bakeJobs.RemoveAt(i);
                }
            }
        }

        public override void CallerDispose() {
            foreach (var item in bakeJobs) {
                item.Item1.Complete();
            }
        }
    }
}