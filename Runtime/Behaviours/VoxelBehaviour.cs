using jedjoud.VoxelTerrain.Generation;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    // Used internally by the classes that handle terrain
    public class VoxelBehaviour : MonoBehaviour {
        [HideInInspector]
        public VoxelTerrain terrain => GetComponent<VoxelTerrain>();
        public long tick => terrain.currentTick;

        public virtual void CallerStart() { }
        public virtual void CallerTick() { }
        public virtual void CallerDispose() { }
    }
}