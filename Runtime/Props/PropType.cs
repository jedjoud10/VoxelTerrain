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
            public Texture2D[] diffuse;
            public Texture2D[] normal;
            public Texture2D[] mask;
            public Vector4 cullingSphere;
        }
                
        [Serializable]
        public class Variant {
            public GameObject prefab;
            public Texture2D diffuse;
            public Texture2D normal;
            public Texture2D mask;
        }

        public List<Variant> variants;

        // if true, spawns entities in the high quality segments
        // if false, spawns instances instead of the entities
        public bool spawnEntities = true;

        // if spawnEntities is false, we can specify *how* we spawn in the instances
        // if this is false, we will not spawn instances for LOD < 0 segments
        public bool alwaysSpawnInstances = true;

        // enables/disables rendering of instances
        public bool renderInstances = true;
        public bool renderInstancesShadow = false;
        public float instanceMaxDistance = 100;
        public bool overrideInstancedIndirectMaterial = false;
        public Material instancedIndirectMaterial = null;
        public Mesh instancedMesh = null;
        public Vector4 cullingSphere = new Vector4(0,0,0, 5);
        
        [Min(1)] public int maxPropsPerSegment = 32 * 32 * 8;
        [Min(1)] public int maxPropsInTotal = 32 * 32 * 32 * 32;
    }
}