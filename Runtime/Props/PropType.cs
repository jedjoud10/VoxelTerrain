using System.Collections.Generic;
using System;
using UnityEngine;
using Unity.Entities;

namespace jedjoud.VoxelTerrain.Props {
    [CreateAssetMenu(menuName = "Voxel Terrain/Create new Voxel Prop")]
    public class PropType : ScriptableObject {
        public class Baked {
            public Entity[] variants;
        }
        
        [Serializable]
        public class Variant {
            public GameObject prefab;
        }

        [Header("Behavior")]
        public List<Variant> variants;
        public PropSpawnBehavior propSpawnBehavior = PropSpawnBehavior.SpawnPrefabs;
        public bool WillSpawnPrefab => propSpawnBehavior.HasFlag(PropSpawnBehavior.SpawnPrefabs);

        [Min(1)] public int maxPropsPerSegment = 32 * 32 * 8;
        [Min(1)] public int maxPropsInTotal = 32 * 32 * 32 * 32;
        //[Min(1)] public int maxVisibleProps = 32 * 32 * 32 * 32;
        //[Min(1)] public float maxInstancingDistance = 1000;
    }

    [Flags]
    public enum PropSpawnBehavior {
        None = 0,

        // Enables/disables rendering far away billboards/instances
        //RenderIndirectInstanced = 1 << 0,

        // Enables/disables spawning in actual prefabs
        SpawnPrefabs = 1 << 1,

        // Replaces EVERYTHING with prefabs
        //SwapForPrefabs = 1 << 2,

        // Swaps out everything for instanced meshes
        // (useful for small rocks or stuff not to be interacted with that we don't want as a gameobject)
        // Only works when we have NO prop prefabs!!!
        //SwapForInstancedMeshes = 1 << 3,
    }
}