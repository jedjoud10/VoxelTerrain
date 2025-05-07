using System.Collections.Generic;
using jedjoud.VoxelTerrain.Octree;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using static jedjoud.VoxelTerrain.VoxelChunk;

namespace jedjoud.VoxelTerrain {
    // compressed iron sheet be like: "hold up... I'm flattened"
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

        public Dictionary<OctreeNode, VoxelChunk> chunks;

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
        public Meshing.VoxelMesher mesher;

        [HideInInspector]
        public Generation.VoxelGraph graph;

        [HideInInspector]
        public Octree.VoxelOctree octree;

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
        private List<GameObject> unusedPooledChunks;

        public void Start() {
            if (materials.Count == 0) {
                throw new System.Exception("Need at least 1 voxel material to be set");
            }

            complete = false;
            disposed = false;
            chunks = new Dictionary<OctreeNode, VoxelChunk>();
            unusedPooledChunks = new List<GameObject>();

            tickDelta = 1 / (float)ticksPerSecond;

            onInit?.Invoke();
            voxelSizeFactor = 1F / Mathf.Pow(2F, voxelSizeReduction);

            collisions = GetComponent<Meshing.VoxelCollisions>();
            mesher = GetComponent<Meshing.VoxelMesher>();
            graph = GetComponent<Generation.VoxelGraph>();
            compiler = GetComponent<Generation.VoxelCompiler>();
            executor = GetComponent<Generation.VoxelExecutor>();
            readback = GetComponent<Generation.VoxelReadback>();
            edits = GetComponent<Edits.VoxelEdits>();
            props = GetComponent<Props.VoxelProps>();
            octree = GetComponent<Octree.VoxelOctree>();

            octree.onOctreeChanged += (ref NativeList<OctreeNode> added, ref NativeList<OctreeNode> removed, ref NativeList<OctreeNode> all) => {
                foreach (var item in removed) {
                    if (chunks.ContainsKey(item)) {
                        PoolChunk(chunks[item].gameObject);
                    }
                }


                foreach (var item in added) {
                    if (item.childBaseIndex != -1)
                        continue;
                    GameObject obj = FetchChunk();
                    VoxelChunk chunk = obj.GetComponent<VoxelChunk>();

                    float size = item.size / (voxelSizeFactor * 64f);
                    obj.GetComponent<MeshRenderer>().enabled = false;
                    obj.transform.position = item.position;
                    obj.transform.localScale = new Vector3(size, size, size);

                    chunk.ResetChunk(item);
                    chunks.Add(item, chunk);

                    // Begin the voxel pipeline by generating the voxels for this chunk
                    readback.GenerateVoxels(chunk);
                }
            };

            readback.onReadback += (VoxelChunk chunk) => {
                mesher.GenerateMesh(chunk, false);
            };
            /*
            spawner.onChunkSpawned += (VoxelChunk chunk) => {
                readback.GenerateVoxels(chunk);
                props.GenerateProps(chunk);
                pendingChunks++;
            };



            mesher.onMeshingComplete += (VoxelChunk chunk, Meshing.VoxelMesh mesh) => collisions.GenerateCollisions(chunk, mesh);

            collisions.onCollisionBakingComplete += (VoxelChunk chunk) => {
                pendingChunks--;
                complete = true;
            };

            onComplete += () => {
                Debug.Log("Terrain generation finished!");
            };
            */

            mesher.CallerStart();
            collisions.CallerStart();
            graph.CallerStart();
            executor.CallerStart();
            props.CallerStart();
            edits.CallerStart();
            readback.CallerStart();
            octree.CallerStart();
        }

        public void Update() {
            accumulator += Time.deltaTime;


            int i = 0;
            while (accumulator >= tickDelta) {
                accumulator -= tickDelta;
                i++;

                Profiler.BeginSample("Octree");
                octree.CallerTick();
                Profiler.EndSample();

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

        public void OnApplicationQuit() {
            Dispose();
        }

        private void OnDisable() {
            Dispose();
        }

        private void Dispose() {
            if (disposed)
                return;

            disposed = true;
            mesher.CallerDispose();
            graph.CallerDispose();
            executor.CallerDispose();
            readback.CallerDispose();
            edits.CallerDispose();
            props.CallerDispose();
            octree.CallerDispose();

            foreach (var (node, chunk) in chunks) {
                chunk.Dispose();
            }

            foreach (var go in unusedPooledChunks) {
                go.GetComponent<VoxelChunk>().Dispose();
            }
        }

        private GameObject FetchChunk() {
            GameObject chunk;

            if (unusedPooledChunks.Count == 0) {
                chunk = Instantiate(chunkPrefab, transform);
                chunk.name = $"Voxel Chunk";
                Mesh mesh = new Mesh();
                VoxelChunk component = chunk.GetComponent<VoxelChunk>();
                component.InitChunk();
                component.sharedMesh = mesh;
            } else {
                chunk = unusedPooledChunks[unusedPooledChunks.Count - 1];
                unusedPooledChunks.RemoveAt(unusedPooledChunks.Count - 1);
                chunk.GetComponent<MeshCollider>().sharedMesh = null;
                chunk.GetComponent<MeshFilter>().sharedMesh = null;
            }

            chunk.SetActive(true);
            return chunk;
        }

        private void PoolChunk(GameObject chunk) {
            chunk.SetActive(false);
            unusedPooledChunks.Add(chunk);
        }


        private void OnDrawGizmosSelected() {
            if (chunks != null && drawGizmos) {
                foreach (var (key, go) in chunks) {
                    VoxelChunk chunk = go.GetComponent<VoxelChunk>();
                    
                    Bounds bounds = chunk.GetBounds();

                    if (bounds == default) {
                        bounds = new Bounds() {
                            center = chunk.node.Center,
                            extents = Vector3.one * 10.0f,
                        };
                    }


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