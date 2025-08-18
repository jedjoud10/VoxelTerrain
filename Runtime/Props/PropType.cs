using System.Collections.Generic;
using System;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Props {
    [CreateAssetMenu(menuName = "Voxel Terrain/Create new Voxel Prop")]
    public class PropType : ScriptableObject {
        public List<GameObject> variants;

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

        // when the main variants don't have a main mesh renderer attached to them
        public Material overrideMaterial;

        // le sus
        public bool renderImpostors = true;

        // at what percent of the total distance should we start rendering impostors 
        public float impostorDistancePercentage = 0.5f;
        public Vector3 impostorOffset = Vector3.zero;
        public float impostorScale = 1f;
        public int impostorTextureWidth = 32;
        public int impostorTextureHeight = 32;
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
            YZ,
            NegativeXZ,
            NegativeXY,
            NegativeYZ,
        }

        public bool impostorInvertAzimuth;

        [Min(1)] public int maxPropsPerSegment = 32 * 32 * 8;
        [Min(1)] public int maxPropsInTotal = 32 * 32 * 32 * 32;
    }
}