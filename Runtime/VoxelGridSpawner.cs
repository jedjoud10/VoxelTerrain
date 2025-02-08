using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class VoxelGridSpawner : VoxelBehaviour {
    public Vector3Int mapChunkSize;

    struct TestJob : IJobParallelFor {
        public NativeArray<Voxel> voxels;
        
        public void Execute(int index) {
            float density = (float)VoxelUtils.IndexToPos(index).y - 5f;
            
            voxels[index] = new Voxel() {
                density = (half)density,
                material = 0,
            };
        }
    }

    public override void LateInit() {
        for (int x = -mapChunkSize.x; x < mapChunkSize.x; x++) {
            for (int y = -mapChunkSize.y; y < mapChunkSize.y; y++) {
                for (int z = -mapChunkSize.z; z < mapChunkSize.z; z++) {
                    Vector3 position = new Vector3(x, y, z) * VoxelUtils.Size * VoxelUtils.VoxelSizeFactor;
                    var container = new UniqueVoxelContainer();
                    VoxelChunk chunk = terrain.FetchChunk(container, position, 1.0f);
                    chunk.dependency = new TestJob { voxels = container.voxels }.Schedule(VoxelUtils.Volume, 2048);
                    terrain.GetBehaviour<VoxelMesher>().GenerateMesh(chunk, true);
                    //callback.Invoke(voxelChunk, index);
                    //totalChunks.Add(newChunk);
                    //index++;
                }
            }
        }
    }
}
