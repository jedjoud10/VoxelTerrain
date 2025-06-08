using UnityEngine;

namespace jedjoud.VoxelTerrain {
    public class ManagedTerrainMainCamera : MonoBehaviour {
        public static ManagedTerrainMainCamera instance;
        void Awake() {
            instance = this;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init() {
            instance = null;
        }
    }
}