using System;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Octree {
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