using UnityEngine;

namespace jedjoud.VoxelTerrain.Demo {
    public class MainCameraGameObject : MonoBehaviour {
        public static MainCameraGameObject instance;
        void Awake() {
            instance = this;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init() {
            instance = null;
        }
    }
}