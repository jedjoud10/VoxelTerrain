using UnityEngine;

namespace jedjoud.VoxelTerrain {
    public class TerrainBehaviour : MonoBehaviour {
        [HideInInspector]
        public Terrain terrain => GetComponent<Terrain>();
        [HideInInspector]
        public long tick => terrain.currentTick;
        [HideInInspector]
        public bool disposed => terrain.disposed;
        public virtual void CallerStart() { }
        public virtual void CallerTick() { }
        public virtual void CallerDispose() { }
    }
}