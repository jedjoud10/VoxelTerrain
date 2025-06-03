using System.Collections.Generic;
using System;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Mono.Cecil;
using Codice.CM.Client.Differences;

namespace jedjoud.VoxelTerrain.Props {
    [CreateAssetMenu(menuName = "Voxel Terrain/Create new Voxel Prop")]
    public class PropType : ScriptableObject {
        [Serializable]
        public class CullingSphereSettings {
            public Vector3 position = Vector3.zero;
            public float radius = 5;

            public Vector4 ToVec4() {
                return new Vector4(position.x, position.y, position.z, radius);
            }
        }

        [Serializable]
        public class Variant {
            public GameObject prefab = null;
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
        public int impostorTextureWidth = 128;
        public int impostorTextureHeight = 128;
        public ImpostorCapturePolarAxis impostorCaptureAxis = ImpostorCapturePolarAxis.XZ;

        [HideInInspector]
        public Texture2DArray impostorDiffuseMaps = null;
        [HideInInspector]
        public Texture2DArray impostorNormalMaps = null;
        [HideInInspector]
        public Texture2DArray impostorMaskMaps = null;

        public enum ImpostorCapturePolarAxis {
            XZ,
            XY,
            YZ
        }

        public enum Axis: int { X = 0, Y = 1, Z = 2, NegativeX = 3, NegativeY = 4, NegativeZ = 5 }

        public Axis impostorNormalX = Axis.X;
        public Axis impostorNormalY = Axis.Y;
        public Axis impostorNormalZ = Axis.Z;

        [Min(1)] public int maxPropsPerSegment = 32 * 32 * 8;
        [Min(1)] public int maxPropsInTotal = 32 * 32 * 32 * 32;
    }
}