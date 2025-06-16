using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    public class TerrainLoader : MonoBehaviour {
        private Vector3 lastPosition;
        public Data data;

        public struct Data {
            [HideInInspector]
            public float3 position;
            public float factor;
            public int3 segmentExtent;
            public int3 segmentExtentHigh;
        }

        private bool registered;

        private void Start() {
            registered = false;
            data.position = transform.position;
        }

        private void Update() {
            data.position = transform.position;

            if (!registered) {
                if (Terrain.Instance != null) {
                    Terrain.Instance.octree.loaders.Add(this);
                    registered = true;
                }
            }

            if (Vector3.Distance(transform.position, lastPosition) > 1) {
                if (Terrain.Instance != null) {
                    Terrain.Instance.octree.ShouldUpdate();
                }
                lastPosition = transform.position;
            }
        }

        void OnDestroy() {
            if (registered) {
                Terrain.Instance.octree.loaders.Remove(this);
            }
        }
    }
}