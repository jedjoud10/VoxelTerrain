using UnityEngine;

namespace jedjoud.VoxelTerrain {
    // Used internally by the classes that handle terrain
    public class VoxelBehaviour : MonoBehaviour {
        // Fetch the parent terrain heheheha
        [HideInInspector]
        public VoxelTerrain terrain => GetComponent<VoxelTerrain>();

        public virtual void CallerStart() { }
        public virtual void CallerTick() { }
        public virtual void CallerDispose() { }
    }
}