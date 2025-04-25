using UnityEngine;

namespace jedjoud.VoxelTerrain {
    public class VoxelGridSpawner : VoxelBehaviour {
        public delegate void OnChunkSpawned(VoxelChunk chunk);
        public event OnChunkSpawned onChunkSpawned;
        // TODO: since we are using octal generation, this MUST be even
        public Vector3Int mapChunkSize;
        public override void CallerStart() {
            for (int x = -mapChunkSize.x; x < mapChunkSize.x; x++) {
                for (int y = -mapChunkSize.y; y < mapChunkSize.y; y++) {
                    for (int z = -mapChunkSize.z; z < mapChunkSize.z; z++) {
                        Vector3Int chunkPosition = new Vector3Int(x, y, z);
                        VoxelChunk chunk = terrain.FetchChunk(chunkPosition, 1.0f);
                        chunk.state = VoxelChunk.ChunkState.Idle;
                        onChunkSpawned?.Invoke(chunk);
                    }
                }
            }
        }

        private void OnDrawGizmosSelected() {
            if (terrain.drawGizmos) 
                Gizmos.DrawWireCube(transform.position, (Vector3)(mapChunkSize * VoxelUtils.SIZE * 2) * terrain.voxelSizeFactor);
        }
    }
}