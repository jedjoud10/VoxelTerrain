using System.Collections.Generic;
using Unity.Jobs;


// Responsible for creating and executing the mesh baking jobs
// Can also be used to check for collisions based on the stored voxel data (needed for props)
public class VoxelCollisions : VoxelBehaviour {
    public delegate void OnCollisionBakingComplete(VoxelChunk chunk, VoxelMesh stats);
    public event OnCollisionBakingComplete onCollisionBakingComplete;
    internal List<(JobHandle, VoxelChunk, VoxelMesh)> ongoingBakeJobs;

    // Initialize the voxel mesher
    public override void Init() {
        ongoingBakeJobs = new List<(JobHandle, VoxelChunk, VoxelMesh)>();
        terrain.GetBehaviour<VoxelMesher>().onVoxelMeshingComplete += HandleVoxelMeshCollision;
    }

    private void HandleVoxelMeshCollision(VoxelChunk chunk, VoxelMesh voxelMesh) {
        if (voxelMesh.VertexCount > 0 && voxelMesh.TriangleCount > 0 && voxelMesh.ComputeCollisions) {
            BakeJob bakeJob = new BakeJob {
                meshId = chunk.sharedMesh.GetInstanceID(),
            };

            var handle = bakeJob.Schedule();
            ongoingBakeJobs.Add((handle, chunk, voxelMesh));
        } else {
            onCollisionBakingComplete?.Invoke(chunk, VoxelMesh.Empty);
        }
    }

    void Update() {
        foreach (var (handle, chunk, mesh) in ongoingBakeJobs) {
            if (handle.IsCompleted) {
                handle.Complete();
                onCollisionBakingComplete?.Invoke(chunk, mesh);
            }
        }
        ongoingBakeJobs.RemoveAll(item => item.Item1.IsCompleted);
    }

    public override void Dispose() {
    }
}
