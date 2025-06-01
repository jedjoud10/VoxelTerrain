using System.Collections.Generic;
using System;
using UnityEngine;
using Unity.Entities;

namespace jedjoud.VoxelTerrain.Props {
    [CreateAssetMenu(menuName = "Voxel Terrain/Create new Voxel Prop")]
    public class PropType : ScriptableObject {
        [Serializable]
        public class Baked {
            public Entity[] prototypes;
        }
                
        [Serializable]
        public class Variant {
            public GameObject prefab;
        }

        public List<Variant> variants;

        // if true, spawns entities in the high quality segments
        // if false, spawns instances instead of the entities
        public bool spawnEntities;

        // enables/disables rendering of instances
        public bool renderInstances;
        public bool renderInstancesShadow;
        public float instanceMaxDistance;

        [Min(1)] public int maxPropsPerSegment = 32 * 32 * 8;
        [Min(1)] public int maxPropsInTotal = 32 * 32 * 32 * 32;

        public Mesh instancedMesh;
        public Material material;
    }
}