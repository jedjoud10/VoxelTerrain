using UnityEngine;

public class VoxelGridSpawner : VoxelBehaviour {
    public delegate void OnChunkSpawned(VoxelChunk chunk);
    public event OnChunkSpawned onChunkSpawned;
    public Vector3Int mapChunkSize;
    public override void CallerStart() {
        for (int x = -mapChunkSize.x; x < mapChunkSize.x; x++) {
            for (int y = -mapChunkSize.y; y < mapChunkSize.y; y++) {
                for (int z = -mapChunkSize.z; z < mapChunkSize.z; z++) {
                    Vector3 position = new Vector3(x, y, z) * VoxelUtils.Size * VoxelUtils.VoxelSizeFactor;
                    VoxelChunk chunk = terrain.FetchChunk(position, 1.0f);
                    onChunkSpawned?.Invoke(chunk);
                }
            }
        }
    }

    private void OnDrawGizmosSelected() {
        Gizmos.DrawWireCube(transform.position, mapChunkSize * VoxelUtils.Size * 2);
    }
}
