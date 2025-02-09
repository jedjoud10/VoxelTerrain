using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using System.Linq;
using Unity.Collections;

public class VoxelEdits : VoxelBehaviour {
    /*
    [Range(1, 8)]
    public int editJobsPerFrame = 1;
    public delegate void OnVoxelEditApplied(VoxelChunk chunk, VoxelMesh mesh);
    public event OnVoxelEditApplied onVoxelMeshingComplete;
    internal Queue<PendingMeshJob> pendingMeshJobs;
    */

    // Apply a voxel edit to the terrain world
    // Could either be used in game (for destructible terrain) or in editor for creating the terrain map
    public void ApplyVoxelEdit(IVoxelEdit edit, bool immediate) {
        Bounds editBounds = edit.GetBounds();

        foreach (var chunk in terrain.totalChunks) {
            var voxelChunk = chunk.GetComponent<VoxelChunk>();

            if (voxelChunk.GetBounds().Intersects(editBounds)) {
                if (voxelChunk.dependency != default) {
                    voxelChunk.dependency.Complete();
                }

                voxelChunk.dependency = edit.Apply(voxelChunk.transform.position, voxelChunk.voxels, new NativeMultiCounter());
                terrain.mesher.GenerateMesh(voxelChunk, immediate);
            }
        }
    }

    public override void CallerStart() {
    }

    public override void CallerUpdate() {
    }

    public override void CallerDispose() {
    }
}
