using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using static jedjoud.VoxelTerrain.VoxelChunk;

namespace jedjoud.VoxelTerrain {
    public class VoxelTerrain : MonoBehaviour {
        [Header("Tick System")]
        public int ticksPerSecond = 128;
        public int maxTicksPerFrame = 3;
        private float tickDelta;
        private float accumulator;
        internal long currentTick;
        internal bool disposed;

        [Header("General")]
        public GameObject chunkPrefab;
        public List<VoxelMaterial> materials;

        [Range(0, 4)]
        public int voxelSizeReduction;
        internal float voxelSizeFactor;

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
            disposed = false;
            totalChunks = new Dictionary<Vector3Int, GameObject>();
            tickDelta = 1 / (float)ticksPerSecond;

            onInit?.Invoke();
            voxelSizeFactor = 1F / Mathf.Pow(2F, voxelSizeReduction);

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

            readback.onReadbackSuccessful += (VoxelChunk chunk, bool empty) => {
                if (empty) {
                    pendingChunks--;
                    complete = true;
                } else {
                    mesher.GenerateMesh(chunk, false);
                }
            };

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

                Profiler.BeginSample("Readback");
                readback.CallerTick();
                Profiler.EndSample();

                Profiler.BeginSample("Props");
                props.CallerTick();
                Profiler.EndSample();

                Profiler.BeginSample("Edits");
                edits.CallerTick();
                Profiler.EndSample();

                Profiler.BeginSample("Mesher");
                mesher.CallerTick();
                Profiler.EndSample();

                Profiler.BeginSample("Collisions");
                collisions.CallerTick();
                Profiler.EndSample();

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
            disposed = true;
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
            chunkGameObject.transform.position = (Vector3)chunkPosition * VoxelUtils.SIZE * voxelSizeFactor;
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
                    Color color = Color.white;

                    switch (chunk.state) {
                        case ChunkState.Idle:
                            color = Color.white;
                            break;
                        case ChunkState.VoxelGeneration:
                            color = Color.red;
                            break;
                        case ChunkState.VoxelReadback:
                            color = Color.yellow;
                            break;
                        case ChunkState.Temp:
                            color = Color.blue;
                            break;
                        case ChunkState.Meshing:
                            color = Color.cyan;
                            break;
                        case ChunkState.Done:
                            color = Color.green;
                            break;
                    }

                    Gizmos.color = color;
                    Gizmos.DrawWireCube(bounds.center, bounds.size);
                }
            }
        }
    }
}