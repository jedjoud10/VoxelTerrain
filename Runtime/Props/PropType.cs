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
            // this field is only visible when SpawnEntities is true
            public GameObject prefab;
            
            public Vector3 cullingCenter;
            public float cullingRadius = 1f;
        }

        [HideInInspector]
        public List<Variant> variants;
        [HideInInspector]
        public PropSpawnBehavior propSpawnBehavior = PropSpawnBehavior.SpawnEntities | PropSpawnBehavior.RenderInstanced;
        public bool SpawnEntities => propSpawnBehavior.HasFlag(PropSpawnBehavior.SpawnEntities);
        public bool RenderInstanced => propSpawnBehavior.HasFlag(PropSpawnBehavior.RenderInstanced);
        public bool InstanceShadow => propSpawnBehavior.HasFlag(PropSpawnBehavior.InstanceShadow);

        [HideInInspector]
        [Min(1)] public int maxPropsPerSegment = 32 * 32 * 8;
        [HideInInspector]
        [Min(1)] public int maxPropsInTotal = 32 * 32 * 32 * 32;

        // these fields is only visible when RenderInstanced is true
        [HideInInspector]
        public Mesh instancedMesh;
        [HideInInspector]
        public Material material;
    }

    [Flags]
    public enum PropSpawnBehavior {
        None = 0,
        SpawnEntities = 1,
        RenderInstanced = 2,
        InstanceShadow = 4,
    }
}