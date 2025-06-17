using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;

namespace jedjoud.VoxelTerrain {
    public class TerrainManager : MonoBehaviour {
        [Header("Tick System")]
        [Min(1)]
        public int ticksPerSecond = 32;
        public int maxTicksPerFrame = 3;

        private float tickDelta;
        private float accumulator;
        internal long currentTick;
        internal bool disposed;

        [Header("General")]
        public GameObject chunkPrefab;
        public GameObject skirtPrefab;

        [Header("Debug")]
        public bool debugGui;
        public bool debugChunkBounds;
        public bool debugSegmentBounds;

        [HideInInspector]
        public Dictionary<Octree.OctreeNode, TerrainChunk> chunks;

        public static TerrainManager Instance {
            get {
                TerrainManager[] terrains = Object.FindObjectsByType<TerrainManager>(FindObjectsSortMode.None);

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
        public Meshing.TerrainCollisions collisions;

        [HideInInspector]
        public Meshing.TerrainMesher mesher;

        [HideInInspector]
        public Generation.TerrainGraph graph;

        [HideInInspector]
        public Octree.TerrainOctree octree;

        [HideInInspector]
        public Generation.TerrainCompiler compiler;

        [HideInInspector]
        public Generation.TerrainSeeder seeder;

        [HideInInspector]
        public Generation.TerrainReadback readback;

        [HideInInspector]
        public Props.TerrainProps props;

        [HideInInspector]
        public Edits.TerrainEdits edits;

        private List<GameObject> unusedPooledChunks;

        private List<GameObject> pendingChunksToHide;
        private List<GameObject> pendingChunksToShow;
        private int pendingValidChunks;
        private bool waitingForSwap;

        public void Start() {
            disposed = false;
            chunks = new Dictionary<Octree.OctreeNode, TerrainChunk>();
            pendingChunksToHide = new List<GameObject>();
            pendingChunksToShow = new List<GameObject>();
            unusedPooledChunks = new List<GameObject>();
            tickDelta = 1 / (float)ticksPerSecond;

            collisions = GetComponent<Meshing.TerrainCollisions>();
            mesher = GetComponent<Meshing.TerrainMesher>();
            graph = GetComponent<Generation.TerrainGraph>();
            compiler = GetComponent<Generation.TerrainCompiler>();
            readback = GetComponent<Generation.TerrainReadback>();
            edits = GetComponent<Edits.TerrainEdits>();
            props = GetComponent<Props.TerrainProps>();
            seeder = GetComponent<Generation.TerrainSeeder>();
            octree = GetComponent<Octree.TerrainOctree>();

            compiler.Parse();

            pendingValidChunks = -1;

            octree.onOctreeChanged += (ref NativeList<Octree.OctreeNode> added, ref NativeList<Octree.OctreeNode> removed, ref NativeList<Octree.OctreeNode> all, ref NativeList<BitField32> neighbourMasks) => {
                pendingChunksToShow.Clear();
                pendingChunksToHide.Clear();
                pendingValidChunks = 0;

                foreach (var item in removed) {
                    if (chunks.TryGetValue(item, out TerrainChunk chunk)) {
                        pendingChunksToHide.Add(chunk.gameObject);
                        chunks.Remove(item);
                    }
                }
                                
                foreach (var item in added) {
                    if (item.childBaseIndex != -1)
                        continue;

                    GameObject obj = FetchChunk();
                    TerrainChunk chunk = obj.GetComponent<TerrainChunk>();
                    chunk.ResetChunk(item, neighbourMasks[item.index]);
                    chunks.Add(item, chunk);

                    readback.GenerateVoxels(chunk);
                    pendingValidChunks++;
                }

                waitingForSwap = true;
            };

            readback.onReadback += (TerrainChunk chunk, bool skipped) => {
                chunk.skipped = skipped;

                if (skipped) {
                    chunk.gameObject.SetActive(false);
                    pendingValidChunks--;
                } else {
                    mesher.GenerateMesh(chunk);
                }
            };

            mesher.onMeshingComplete += (TerrainChunk chunk, Meshing.MeshJobHandler.Stats stats) => {
                pendingValidChunks--;
                pendingChunksToShow.Add(chunk.gameObject);

                if (chunk.node.depth == octree.maxDepth && !stats.empty)
                    collisions.GenerateCollisions(chunk);
            };

            mesher.CallerStart();
            collisions.CallerStart();
            props.CallerStart();
            edits.CallerStart();
            readback.CallerStart();
            octree.CallerStart();
        }

        public void Update() {
            if (pendingValidChunks == 0) {
                foreach (var item in pendingChunksToHide) {
                    PoolChunk(item.gameObject);
                }

                foreach (var item in pendingChunksToShow) {
                    item.SetActive(true);
                }

                waitingForSwap = false;
                octree.continuousCheck = true;

                pendingChunksToShow.Clear();
                pendingChunksToHide.Clear();
                pendingValidChunks = -1;
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
            readback.CallerDispose();
            edits.CallerDispose();
            props.CallerDispose();
            octree.CallerDispose();

            foreach (var (node, chunk) in chunks) {
                chunk.Dispose();
            }

            foreach (var go in unusedPooledChunks) {
                go.GetComponent<TerrainChunk>().Dispose();
            }
        }

        private GameObject FetchChunk() {
            GameObject chunkGo;

            if (unusedPooledChunks.Count == 0) {
                chunkGo = Instantiate(chunkPrefab, transform);
                chunkGo.name = $"Voxel Chunk";
                TerrainChunk component = chunkGo.GetComponent<TerrainChunk>();

                component.voxels = new VoxelData(Allocator.Persistent);
                component.skipIfEmpty = true;
                component.sharedMesh = new Mesh();

                GameObject skirtGo = Instantiate(skirtPrefab, chunkGo.transform);
                skirtGo.transform.localPosition = Vector3.zero;
                skirtGo.transform.localScale = Vector3.one;
                component.skirt = skirtGo;
            } else {
                chunkGo = unusedPooledChunks[unusedPooledChunks.Count - 1];
                unusedPooledChunks.RemoveAt(unusedPooledChunks.Count - 1);
                chunkGo.GetComponent<MeshCollider>().sharedMesh = null;
                chunkGo.GetComponent<MeshFilter>().sharedMesh = null;
            }

            chunkGo.SetActive(false);

            return chunkGo;
        }

        private void PoolChunk(GameObject chunk) {
            chunk.SetActive(false);
            unusedPooledChunks.Add(chunk);
        }

        private void OnGUI() {
            if (!debugGui || chunks == null)
                return;

            var offset = 0;
            List<string> cachedLabels = new List<string>();
            void Label(string text) {
                cachedLabels.Add(text);
                offset += 15;
            }

            void MakeMyShitFuckingOpaqueHolyShitUnityWhyCantYouSupportThisByDefaultThisIsStupid() {
                for (int i = 0; i < 5; i++) {
                    GUI.Box(new Rect(0, 0, 300, offset + 20), "");
                }
            }

            GUI.contentColor = Color.white;

            int total = chunks.Count;
            int awaitingReadback = readback.InQueue;
            int awaitingMeshing = mesher.InQueue;
            int skippedChunks = chunks.Where(x => x.Value.skipped).Count();

            Label($"# of total chunk entities: {total}");
            Label($"# of chunks pending GPU voxel data: {awaitingReadback}");
            Label($"# of chunks pending meshing: {awaitingMeshing}");
            Label($"% of skipped chunks: {100*((float)skippedChunks / (float)total):F1}");

            Label($"Manager System Ready: " + true);
            Label($"Readback System Ready: " + readback.Free);
            Label($"Mesher System Ready: " + mesher.Free);

            /*
            EntityQuery totalChunks = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk));
            EntityQuery meshedChunks = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkMesh));
            EntityQuery chunksAwaitingReadback = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkRequestReadbackTag));
            EntityQuery chunksAwaitingMeshing = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkRequestMeshingTag));
            EntityQuery chunksEndOfPipe = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkEndOfPipeTag));
            EntityQuery segmentsAwaitingDispatch = world.EntityManager.CreateEntityQuery(typeof(TerrainSegment), typeof(TerrainSegmentRequestVoxelsTag));

            SegmentPropStuffSystem system = world.GetExistingSystemManaged<SegmentPropStuffSystem>();

            GUI.contentColor = Color.white;
            Label($"# of total chunk entities: {totalChunks.CalculateEntityCount()}");
            Label($"# of chunks pending GPU voxel data: {chunksAwaitingReadback.CalculateEntityCount()}");
            Label($"# of segments pending GPU dispatch: {segmentsAwaitingDispatch.CalculateEntityCount()}");
            Label($"# of chunks pending meshing: {chunksAwaitingMeshing.CalculateEntityCount()}");
            Label($"# of chunk entities with a mesh: {meshedChunks.CalculateEntityCount()}");
            Label($"# of chunk entities in the \"End of Pipe\" stage: {chunksEndOfPipe.CalculateEntityCount()}");

            if (system.initialized) {
                TerrainPropPermBuffers.DebugCounts[] counts = system.perm.GetCounts(system.config, system.temp, system.render);
                for (int i = 0; i < counts.Length; i++) {
                    TerrainPropPermBuffers.DebugCounts debug = counts[i];
                    Label($"--- Prop Type {i}: {system.config.props[i].name} ---");
                    Label($"Perm buffer count: {debug.maxPerm}");
                    Label($"Perm buffer offset: {debug.permOffset}");
                    Label($"Temp buffer offset: {debug.maxTemp}");
                    Label($"Temp buffer offset: {debug.tempOffset}");

                    Label($"In-use perm props: {debug.currentInUse}");
                    Label($"Visible instances: {debug.visibleInstances}");
                    Label($"Visible impostors: {debug.visibleImpostors}");
                    Label($"");
                }
            }


            EntityQuery readySystems = world.EntityManager.CreateEntityQuery(typeof(TerrainReadySystems));
            TerrainReadySystems ready = readySystems.GetSingleton<TerrainReadySystems>();
            Label($"Manager System Ready: " + ready.manager);
            Label($"Readback System Ready: " + ready.readback);
            Label($"Mesher System Ready: " + ready.mesher);
            Label($"Segment Manager System Ready: " + ready.segmentManager);
            Label($"Segment Voxels System Ready: " + ready.segmentVoxels);
            Label($"Segment Props System Ready: " + ready.segmentPropsDispatch);
            */

            MakeMyShitFuckingOpaqueHolyShitUnityWhyCantYouSupportThisByDefaultThisIsStupid();

            offset = 0;
            foreach (var item in cachedLabels) {
                GUI.Label(new Rect(0, offset, 300, 30), item);
                offset += 15;
            }

        }

        private void OnDrawGizmos() {
            if (chunks == null)
                return;

            if (debugChunkBounds) {
                Gizmos.color = Color.grey;
                foreach (var chunk in chunks) {
                    Bounds bounds = chunk.Value.GetComponent<MeshRenderer>().bounds;
                    Gizmos.DrawWireCube(bounds.center, bounds.extents * 2f);
                }
            }

            /*
            if (world == null)
                return;

            EntityQuery meshedChunks = world.EntityManager.CreateEntityQuery(typeof(TerrainChunk), typeof(TerrainChunkMesh), typeof(WorldRenderBounds));
            EntityQuery segmentsQuery = world.EntityManager.CreateEntityQuery(typeof(TerrainSegment));



            if (debugSegmentBounds) {

                NativeArray<TerrainSegment> segments = segmentsQuery.ToComponentDataArray<TerrainSegment>(Allocator.Temp);
                foreach (var segment in segments) {
                    float3 worldPosition = ((float3)(segment.position) + 0.5f) * SegmentUtils.PHYSICAL_SEGMENT_SIZE;
                    float3 worldSize = new float3(1) * SegmentUtils.PHYSICAL_SEGMENT_SIZE;

                    if (segment.lod == TerrainSegment.LevelOfDetail.Low) {
                        Gizmos.color = Color.green;
                    } else {
                        Gizmos.color = Color.red;
                    }

                    Gizmos.DrawWireCube(worldPosition, worldSize);
                }
            }
            */
        }
    }
}