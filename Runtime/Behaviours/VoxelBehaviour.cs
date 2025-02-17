using jedjoud.VoxelTerrain.Generation;
using UnityEngine;

namespace jedjoud.VoxelTerrain {
    // Used internally by the classes that handle terrain
    public class VoxelBehaviour : MonoBehaviour {
        [HideInInspector]
        public VoxelTerrain terrain => GetComponent<VoxelTerrain>();
        [HideInInspector]
        public VoxelGraph graph => GetComponent<VoxelGraph>();

        public virtual void CallerStart() { }
        public virtual void CallerTick() { }
        public virtual void CallerDispose() { }
    }
}