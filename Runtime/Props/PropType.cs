using System.Collections.Generic;
using System;
using UnityEngine;
using Unity.Entities;

namespace jedjoud.VoxelTerrain.Props {
    [CreateAssetMenu(menuName = "Voxel Terrain/Create new Voxel Prop")]
    public class PropType : ScriptableObject {
        public class Baked {
            public Entity[] prototypes;
            /*
            public Mesh[] meshes;
            public Texture2D[] diffuseTexs;
            public Texture2D[] normalTexs;
            public Texture2D[] maskTexs;
            */
        }
        
        [Serializable]
        public class Variant {
            public GameObject prefab;
        }

        public List<Variant> variants;
        public PropSpawnBehavior propSpawnBehavior = PropSpawnBehavior.SpawnEntities | PropSpawnBehavior.RenderInstanced;
        public bool SpawnEntities => propSpawnBehavior.HasFlag(PropSpawnBehavior.SpawnEntities);
        public bool RenderInstanced => propSpawnBehavior.HasFlag(PropSpawnBehavior.RenderInstanced);

        [Min(1)] public int maxPropsPerSegment = 32 * 32 * 8;
        [Min(1)] public int maxPropsInTotal = 32 * 32 * 32 * 32;
        //[Min(1)] public int maxVisibleProps = 32 * 32 * 32 * 32;
        //[Min(1)] public float maxInstancingDistance = 1000;
        public Mesh instancedMesh;
        public Material material;
    }

    [Flags]
    public enum PropSpawnBehavior {
        None = 0,
        SpawnEntities = 1,
        RenderInstanced = 2
    }
}