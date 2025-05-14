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
        [Min(1)]
        public int ticksPerSecond = 128;

        [Min(1)]
        public int maxTicksPerFrame = 3;
        private float tickDelta;
        private float accumulator;
        internal long currentTick;
        internal bool disposed;

        [Header("General")]
        public GameObject chunkPrefab;
        public GameObject stitchingPrefab;
        public List<VoxelMaterial> materials;
        public float ditherTransitionTime = 0.2f;

        [Range(0, 4)]
        public int voxelSizeReduction;
        internal float voxelSizeFactor;

        [Header("Debug")]
        public bool drawGizmos;
        public bool debugGUI;

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

        private bool complete;
        private List<GameObject> unusedPooledChunks;

        private List<GameObject> pendingChunksToHide;
        private List<GameObject> pendingChunksToShow;

        private int pendingValidChunks;
        private bool waitingForSwap;
        private bool ditherTransition;
        private float transitionBeginTime;


        public void Start() {
            pendingChunksToShow = new List<GameObject>();
            pendingChunksToHide = new List<GameObject>();

            if (ticksPerSecond < 0 | maxTicksPerFrame < 0) {
                Debug.Log("bro is trying too hard");
                ticksPerSecond = 64;
                maxTicksPerFrame = 1;
            }

            if (materials.Count == 0) {
                throw new System.Exception("Need at least 1 voxel material to be set");
            }

            complete = false;
            disposed = false;
            chunks = new Dictionary<OctreeNode, VoxelChunk>();
            unusedPooledChunks = new List<GameObject>();
            tickDelta = 1 / (float)ticksPerSecond;

            onInit?.Invoke();
            voxelSizeFactor = 1F / (1 << voxelSizeReduction);
            waitingForSwap = false;

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
                pendingChunksToShow.Clear();
                pendingChunksToHide.Clear();

                foreach (var item in removed) {
                    if (chunks.ContainsKey(item)) {
                        pendingChunksToHide.Add(chunks[item].gameObject);
                        chunks.Remove(item);
                    }
                }
                                
                foreach (var item in added) {
                    if (item.childBaseIndex != -1)
                        continue;

                    GameObject obj = FetchChunk();
                    VoxelChunk chunk = obj.GetComponent<VoxelChunk>();

                    float size = item.size / (voxelSizeFactor * 64f);
                    obj.GetComponent<MeshRenderer>().enabled = false;
                    obj.transform.position = math.float3(item.position);
                    obj.transform.localScale = new Vector3(size, size, size);

                    chunk.ResetChunk(item);
                    chunks.Add(item, chunk);

                    readback.GenerateVoxels(chunk);
                    pendingValidChunks++;
                }

                waitingForSwap = true;
                octree.continuousCheck = false;
                ditherTransition = false;
            };

            readback.onReadback += (VoxelChunk chunk, bool skipped) => {
                chunk.skipped = skipped;
                if (skipped) {
                    chunk.state = VoxelChunk.ChunkState.Done;
                    chunk.gameObject.SetActive(false);
                    pendingValidChunks--;
                } else {
                    chunk.state = VoxelChunk.ChunkState.Temp;
                    mesher.GenerateMesh(chunk, false);
                }
            };

            mesher.onMeshingComplete += (VoxelChunk chunk, Meshing.VoxelMesh mesh) => {
                if (mesh.VertexCount > 0 && mesh.TriangleCount > 0) {
                    collisions.GenerateCollisions(chunk, mesh);
                    pendingChunksToShow.Add(chunk.gameObject);
                }
                pendingValidChunks--;
            };

            /*
            spawner.onChunkSpawned += (VoxelChunk chunk) => {
                readback.GenerateVoxels(chunk);
                props.GenerateProps(chunk);
                pendingChunks++;
            };




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

        private void SetTransitionDither(float ditherIn) {
            foreach (var item in pendingChunksToShow) {
                item.GetComponent<VoxelChunk>().SetDither(ditherIn);
            }

            foreach (var item in pendingChunksToHide) {
                item.GetComponent<VoxelChunk>().SetDither(-ditherIn);
            }
        }

        public void Update() {
            if (ditherTransition) {
                float diff = (Time.unscaledTime - transitionBeginTime) / ditherTransitionTime;
                //Debug.Log($"diff: {diff}");


                if (diff < 1) {
                    SetTransitionDither(diff);
                } else {
                    SetTransitionDither(1f);

                    foreach (var item in pendingChunksToHide) {
                        PoolChunk(item.gameObject);
                    }

                    waitingForSwap = false;
                    octree.continuousCheck = true;

                    pendingChunksToShow.Clear();
                    pendingChunksToHide.Clear();

                    transitionBeginTime = 0f;
                    ditherTransition = false;
                }
            }

            if (waitingForSwap && pendingValidChunks == 0 && !ditherTransition) {
                transitionBeginTime = Time.unscaledTime;
                ditherTransition = true;

                foreach (var item in pendingChunksToShow) {
                    item.SetActive(true);
                }

                SetTransitionDither(-1);
            }

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

                if (i > maxTicksPerFrame) {
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

                GameObject stitchGo = Instantiate(stitchingPrefab, chunk.transform);
                stitchGo.transform.localPosition = Vector3.zero;
                stitchGo.transform.localScale = Vector3.one;
                component.skirt = stitchGo.GetComponent<Meshing.VoxelSkirt>();
                component.skirt.source = component;
            } else {
                chunk = unusedPooledChunks[unusedPooledChunks.Count - 1];
                unusedPooledChunks.RemoveAt(unusedPooledChunks.Count - 1);
                chunk.GetComponent<MeshCollider>().sharedMesh = null;
                chunk.GetComponent<MeshFilter>().sharedMesh = null;
            }

            chunk.SetActive(false);

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

        // Used for debugging the amount of jobs remaining
        void OnGUI() {
            var offset = 0;
            void Label(string text) {
                GUI.Label(new Rect(0, offset, 300, 30), text);
                offset += 15;
            }

            if (debugGUI) {
                GUI.Box(new Rect(0, 0, 300, 345), "");
                Label($"# of chunks pending GPU voxel data: {readback.queued.Count}");
                Label($"# of pending mesh jobs: {mesher.queuedMeshingRequests.Count}");
                Label($"# of total chunk game objects: {chunks.Count}");
                Label($"# of unused pooled chunk game objects: {unusedPooledChunks.Count}");

                /*
                Label($"# of pending GPU async readback jobs: {readback.pendingVoxelGenerationChunks.Count}");
                Label($"# of pending mesh jobs: {VoxelMesher.pendingMeshJobs.Count}");
                Label($"# of pending mesh baking jobs: {VoxelCollisions.ongoingBakeJobs.Count}");
                Label($"# of pending voxel segments jobs: {VoxelSegments.pendingSegments.Count}");
                Label($"# of pooled chunk game objects: {pooledChunkGameObjects.Count}");
                Label($"# of pooled native voxel arrays: {pooledVoxelChunkContainers.Count}");

                int usedVoxelArrays = Chunks.Where(x => x.Value.container is UniqueVoxelChunkContainer).Count();
                Label($"# of free native voxel arrays (voxel generator): {VoxelGenerator.freeVoxelNativeArrays.Cast<bool>().Where(x => x).Count()}");
                Label($"# of used native voxel arrays: {usedVoxelArrays}");
                Label($"# of chunks to make visible: {toMakeVisible.Count}");
                Label($"# of enabled chunks: {Chunks.Where(x => x.Value.gameObject.activeSelf).Count()}");
                Label($"# of enabled and meshed chunks: {Chunks.Where(x => (x.Value.gameObject.activeSelf && x.Value.sharedMesh.subMeshCount > 0)).Count()}");
                Label($"# of chunks to remove: {toRemoveChunk.Count}");
                Label($"# of world edits: {VoxelEdits.worldEditRegistry.TryGetAll<IDynamicEdit>().Count}");
                Label($"# of pending voxel edits: {VoxelEdits.tempVoxelEdits.Count}");
                int mul = Voxel.size * VoxelUtils.Volume;
                int bytes = pooledVoxelChunkContainers.Count * mul;
                int kbs = bytes / 1024;
                Label($"KBs of pooled native voxel arrays: {kbs}");
                bytes = usedVoxelArrays * mul;
                int kbs2 = bytes / 1024;
                Label($"KBs of used native voxel arrays: {kbs2}");
                Label($"KBs of total native voxel arrays: {kbs + kbs2}");
                Label("Generator free: " + VoxelGenerator.Free);
                Label("Mesher free: " + VoxelMesher.Free);
                Label("Octree free: " + VoxelOctree.Free);
                Label("Edits free: " + VoxelEdits.Free);
                Label("Props free: " + VoxelProps.Free);
                */
            }
        }
    }
}