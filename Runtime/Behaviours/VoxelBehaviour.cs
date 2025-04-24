using UnityEngine;

namespace jedjoud.VoxelTerrain {
    // Used internally by the classes that handle terrain
    public class VoxelBehaviour : MonoBehaviour {
        [HideInInspector]
        public VoxelTerrain terrain => GetComponent<VoxelTerrain>();
        [HideInInspector]
        public long tick => terrain.currentTick;
        [HideInInspector]
        public bool disposed => terrain.disposed;
        public virtual void CallerStart() { }
        public virtual void CallerTick() { }
        public virtual void CallerDispose() { }
    }
}