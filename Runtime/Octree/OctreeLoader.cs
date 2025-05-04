using System;
using Unity.Mathematics;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Octree {
    public class OctreeLoader : MonoBehaviour {
        // Should be a struct so we can send it to the Unity Jobs easily
        [Serializable]
        public struct Target {
            public bool generateCollisions;

            [HideInInspector]
            public float3 center;

            [Min(0.001F)]
            public float radius;
        }

        public Target data;
    }
}