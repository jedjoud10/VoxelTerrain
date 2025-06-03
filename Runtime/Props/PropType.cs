using System.Collections.Generic;
using System;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace jedjoud.VoxelTerrain.Props {
    [CreateAssetMenu(menuName = "Voxel Terrain/Create new Voxel Prop")]
    public class PropType : ScriptableObject {
        [Serializable]
        public class BillboardCaptureSettings {
            public float cameraScale = 10.0f;
            public Vector3 rotation = Vector3.zero;
            public Vector3 position = new Vector3(0, 0, 5);
        }

        [Serializable]
        public class CullingSphereSettings {
            public Vector3 position = Vector3.zero;
            public float radius = 5;

            public Vector4 ToVec4() {
                return new Vector4(position.x, position.y, position.z, radius);
            }
        }

        [Serializable]
        public class InstancedTextures {
            public Texture2D diffuse = null;
            public Texture2D normal = null;
            public Texture2D mask = null;
        }

        [Serializable]
        public class Variant {
            public GameObject prefab = null;
            public InstancedTextures textures = null;
            public BillboardCaptureSettings billboardCapture = null;
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
        public Mesh instancedMesh = null;

        // le sus
        public bool renderImpostors = true;

        // at what percent of the total distance should we start rendering impostors 
        public float impostorDistancePercentage = 0.5f;
        public Vector3 impostorOffset = Vector3.zero;
        public float impostorScale = 1f;

        [Min(1)] public int maxPropsPerSegment = 32 * 32 * 8;
        [Min(1)] public int maxPropsInTotal = 32 * 32 * 32 * 32;
    }
}