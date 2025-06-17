using jedjoud.VoxelTerrain.Octree;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    public partial class TerrainChunk : MonoBehaviour {
        public VoxelData voxels;
        public JobHandle asyncReadJobHandle;
        public JobHandle asyncWriteJobHandle;

        public OctreeNode node;
        [HideInInspector]
        public Mesh sharedMesh;
        [HideInInspector]
        public GameObject skirt;
        [HideInInspector]
        public BitField32 neighbourMask;
        [HideInInspector]
        public bool skipIfEmpty;
        [HideInInspector]
        public bool skipped;

        public void ResetChunk(OctreeNode node, BitField32 neighbourMask) {
            float size = node.size / (VoxelUtils.PHYSICAL_CHUNK_SIZE);
            gameObject.GetComponent<MeshRenderer>().enabled = false;
            gameObject.transform.position = (Vector3)math.float3(node.position);
            gameObject.transform.localScale = Vector3.one * size;
            gameObject.name = node.position.ToString();

            this.node = node;
            this.neighbourMask = neighbourMask;
            skipped = false;

            asyncReadJobHandle.Complete();
            asyncWriteJobHandle.Complete();

            asyncReadJobHandle = default;
            asyncWriteJobHandle = default;
        }

        public void Dispose() {
            asyncReadJobHandle.Complete();
            asyncWriteJobHandle.Complete();
            voxels.Dispose();
        }
    }
}