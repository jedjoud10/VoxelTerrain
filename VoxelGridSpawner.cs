using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelGridSpawner : VoxelBehaviour {
    public Vector3Int mapChunkSize;
    
    public override void LateInit() {
        for (int x = -mapChunkSize.x; x < mapChunkSize.x; x++) {
            for (int y = -mapChunkSize.y; y < mapChunkSize.y; y++) {
                for (int z = -mapChunkSize.z; z < mapChunkSize.z; z++) {
                    Vector3 position = new Vector3(x, y, z) * VoxelUtils.Size * VoxelUtils.VoxelSizeFactor;
                    var container = new UniqueVoxelContainer();
                    VoxelChunk chunk = terrain.FetchPooledChunk(container, position, 1.0f);
                    // chunk.dependency = 
                    //callback.Invoke(voxelChunk, index);
                    //totalChunks.Add(newChunk);
                    //index++;
                }
            }
        }
    }
}
