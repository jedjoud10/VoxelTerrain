using System.Collections.Generic;
using Unity.Jobs;

namespace jedjoud.VoxelTerrain.Meshing {
    // Responsible for creating and executing the mesh baking jobs
    // Can also be used to check for collisions based on the stored voxel data (needed for props)
    public class VoxelCollisions : VoxelBehaviour {
        public delegate void OnCollisionBakingComplete(VoxelChunk chunk);
        public event OnCollisionBakingComplete onCollisionBakingComplete;
        internal List<(JobHandle, VoxelChunk, VoxelMesh)> ongoingBakeJobs;

        public override void CallerStart() {
            ongoingBakeJobs = new List<(JobHandle, VoxelChunk, VoxelMesh)>();
        }

        public void GenerateCollisions(VoxelChunk chunk, VoxelMesh voxelMesh) {
            if (voxelMesh.VertexCount > 0 && voxelMesh.TriangleCount > 0 && voxelMesh.ComputeCollisions) {
                BakeJob bakeJob = new BakeJob {
                    meshId = chunk.sharedMesh.GetInstanceID(),
                };

                var handle = bakeJob.Schedule();
                ongoingBakeJobs.Add((handle, chunk, voxelMesh));
            } else {
                onCollisionBakingComplete?.Invoke(chunk);
            }
        }

        public override void CallerUpdate() {
            foreach (var (handle, chunk, mesh) in ongoingBakeJobs) {
                if (handle.IsCompleted) {
                    handle.Complete();
                    onCollisionBakingComplete?.Invoke(chunk);
                }
            }
            ongoingBakeJobs.RemoveAll(item => item.Item1.IsCompleted);
        }
    }
}