using System;
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

        public void ResetChunk(OctreeNode item, BitField32 neighbourMask) {
            float size = item.size / (VoxelUtils.PHYSICAL_CHUNK_SIZE);
            gameObject.GetComponent<MeshRenderer>().enabled = false;
            gameObject.transform.position = (Vector3)math.float3(item.position);
            gameObject.transform.localScale = Vector3.one * size;
            gameObject.name = item.position.ToString();

            this.node = item;
            this.neighbourMask = neighbourMask;
        }

        public void Dispose() {
            voxels.Dispose();
        }
    }
}