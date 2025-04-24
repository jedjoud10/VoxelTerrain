using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using static jedjoud.VoxelTerrain.VoxelTerrain;

namespace jedjoud.VoxelTerrain {
    public class VoxelTerrain : MonoBehaviour {
        [Header("Tick System")]
        public int ticksPerSecond = 128;
        public int maxTicksPerFrame = 3;
        private float tickDelta;
        private float accumulator;
        internal long currentTick;

        [Header("General")]
        public GameObject chunkPrefab;
        public List<VoxelMaterial> materials;

        [Range(0, 4)]
        public int voxelSizeReduction;

        [Header("Debug")]
        public bool drawGizmos;

        [HideInInspector]
        public Dictionary<Vector3Int, GameObject> totalChunks;
        public static VoxelTerrain Instance {
            get {
                VoxelTerrain[] terrains = Object.FindObjectsByType<VoxelTerrain>(FindObjectsSortMode.None);

                if (terrains.Length > 1) {
                    Debug.LogWarning("Can't have more than one VoxelTerrain per scene!");
                    return null;
                } else if (terrains.Length == 0) {
                    return null;
                } else {
                    return terrains[0];
                }
            }
        }

        [HideInInspector]
        public Meshing.VoxelCollisions collisions;

        [HideInInspector]
        public VoxelGridSpawner spawner;

        [HideInInspector]
        public Meshing.VoxelMesher mesher;

        [HideInInspector]
        public Generation.VoxelGraph graph;

        [HideInInspector]
        public Generation.VoxelCompiler compiler;

        [HideInInspector]
        public Generation.VoxelExecutor executor;

        [HideInInspector]
        public Generation.VoxelReadback readback;

        [HideInInspector]
        public Props.VoxelProps props;

        [HideInInspector]
        public Edits.VoxelEdits edits;

        public delegate void OnInit();
        public event OnInit onInit;

        public delegate void OnCompleted();
        public event OnCompleted onComplete;
        private int pendingChunks;
        private bool complete;

        public void Start() {
            if (materials.Count == 0) {
                throw new System.Exception("Need at least 1 voxel material to be set");
            }

            complete = false;
            //Instance = this;
            totalChunks = new Dictionary<Vector3Int, GameObject>();
            tickDelta = 1 / (float)ticksPerSecond;

            onInit?.Invoke();
            VoxelUtils.SchedulingInnerloopBatchCount = 64;

            collisions = GetComponent<Meshing.VoxelCollisions>();
            spawner = GetComponent<VoxelGridSpawner>();
            mesher = GetComponent<Meshing.VoxelMesher>();
            graph = GetComponent<Generation.VoxelGraph>();
            compiler = GetComponent<Generation.VoxelCompiler>();
            executor = GetComponent<Generation.VoxelExecutor>();
            readback = GetComponent<Generation.VoxelReadback>();
            edits = GetComponent<Edits.VoxelEdits>();
            props = GetComponent<Props.VoxelProps>();

            spawner.onChunkSpawned += (VoxelChunk chunk) => {
                readback.GenerateVoxels(chunk);
                props.GenerateProps(chunk);
                pendingChunks++;
            };

            readback.onReadbackSuccessful += (VoxelChunk chunk) => mesher.GenerateMesh(chunk, false);

            mesher.onVoxelMeshingComplete += (VoxelChunk chunk, Meshing.VoxelMesh mesh) => collisions.GenerateCollisions(chunk, mesh);

            collisions.onCollisionBakingComplete += (VoxelChunk chunk) => {
                pendingChunks--;
                complete = true;
            };

            onComplete += () => {
                Debug.Log("Terrain generation finished!");
                /*
                edits.ApplyVoxelEdit(new jedjoud.VoxelTerrain.Edits.SphereVoxelEdit {
                    strength = 100,
                    center = new Unity.Mathematics.float3(10, 10, 10),
                    material = 0,
                    paintOnly = false,
                    radius = 10f,
                    writeMaterial = true,
                }, false);
                */
            };

            mesher.CallerStart();
            collisions.CallerStart();
            graph.CallerStart();
            executor.CallerStart();
            props.CallerStart();
            edits.CallerStart();
            readback.CallerStart();
            spawner.CallerStart();
        }

        public void Update() {
            accumulator += Time.deltaTime;


            int i = 0;
            while (accumulator >= tickDelta) {
                accumulator -= tickDelta;
                i++;

                readback.CallerTick();
                props.CallerTick();
                edits.CallerTick();
                mesher.CallerTick();
                collisions.CallerTick();

                if (complete && pendingChunks == 0) {
                    complete = false;
                    onComplete?.Invoke();
                }

                if (i >= maxTicksPerFrame) {
                    accumulator = 0;
                    break;
                }

                currentTick++;
            }
        }

        public void OnValidate() {
            VoxelUtils.VoxelSizeReduction = voxelSizeReduction;
        }

        /*
        public void OnApplicationQuit() {
            mesher.CallerDispose();
            graph.CallerDispose();
            executor.CallerDispose();
            readback.CallerDispose();
            edits.CallerDispose();
            props.CallerDispose();

            foreach (var (key, value) in totalChunks) {
                VoxelChunk voxelChunk = value.GetComponent<VoxelChunk>();
                voxelChunk.dependency?.Complete();
                voxelChunk.voxels.Dispose();
            }
        }
        */

        private void OnDisable() {
            mesher.CallerDispose();
            graph.CallerDispose();
            executor.CallerDispose();
            readback.CallerDispose();
            edits.CallerDispose();
            props.CallerDispose();

            foreach (var (key, value) in totalChunks) {
                VoxelChunk voxelChunk = value.GetComponent<VoxelChunk>();
                voxelChunk.voxels.Dispose();
            }
        }

        // Instantiates a new chunk and returns it
        public VoxelChunk FetchChunk(Vector3Int chunkPosition, float scale) {
            VoxelChunk chunk;

            GameObject obj = Instantiate(chunkPrefab, transform);
            obj.name = $"Voxel Chunk";

            Mesh mesh = new Mesh();
            chunk = obj.GetComponent<VoxelChunk>();
            chunk.sharedMesh = mesh;

            GameObject chunkGameObject = chunk.gameObject;
            chunkGameObject.transform.position = (Vector3)chunkPosition * VoxelUtils.SIZE * VoxelUtils.VoxelSizeFactor;
            chunkGameObject.transform.localScale = scale * Vector3.one;
            chunk.chunkPosition = chunkPosition;
            chunk.voxels = new NativeArray<Voxel>(VoxelUtils.VOLUME, Allocator.Persistent);
            totalChunks.Add(chunkPosition, chunkGameObject);
            return chunk;
        }

        private void OnDrawGizmosSelected() {
            if (totalChunks != null && drawGizmos) {
                foreach (var (key, go) in totalChunks) {
                    VoxelChunk chunk = go.GetComponent<VoxelChunk>();
                    
                    Bounds bounds = chunk.GetBounds();
                    if (chunk.sharedMesh != null && chunk.sharedMesh.vertexCount > 0) {
                        //Bounds bounds = chunk.GetComponent<MeshRenderer>().bounds;

                        Gizmos.color = Color.white;
                        Gizmos.DrawWireCube(bounds.center, bounds.size);
                    } else if (chunk.HasVoxelData()) {
                        Gizmos.color = Color.red;
                        Gizmos.DrawWireCube(bounds.center, bounds.size);
                    }
                }
            }
        }
    }
}