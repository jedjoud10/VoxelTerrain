using UnityEngine;

namespace jedjoud.VoxelTerrain {
    // Used internally by the classes that handle terrain
    public class VoxelBehaviour : MonoBehaviour {
        // Fetch the parent terrain heheheha
        [HideInInspector]
        public VoxelTerrain terrain;

        public virtual void CallerStart() { }
        public virtual void CallerUpdate() { }
        public virtual void CallerDispose() { }
    }
}