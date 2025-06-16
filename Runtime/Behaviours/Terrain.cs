using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;

namespace jedjoud.VoxelTerrain {
    public class Terrain : MonoBehaviour {
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
        public List<TerrainMaterial> materials;

        public Dictionary<Octree.OctreeNode, TerrainChunk> chunks;

        public static Terrain Instance {
            get {
                Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);

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
        
        public void Start() {
            if (materials.Count == 0) {
                throw new System.Exception("Need at least 1 voxel material to be set");
            }

            disposed = false;
            chunks = new Dictionary<Octree.OctreeNode, TerrainChunk>();
            unusedPooledChunks = new List<GameObject>();
            tickDelta = 1 / (float)ticksPerSecond;

            collisions = GetComponent<Meshing.TerrainCollisions>();
            mesher = GetComponent<Meshing.TerrainMesher>();
            graph = GetComponent<Generation.TerrainGraph>();
            compiler = GetComponent<Generation.TerrainCompiler>();
            readback = GetComponent<Generation.TerrainReadback>();
            edits = GetComponent<Edits.TerrainEdits>();
            props = GetComponent<Props.TerrainProps>();
            octree = GetComponent<Octree.TerrainOctree>();

            octree.onOctreeChanged += (ref NativeList<Octree.OctreeNode> added, ref NativeList<Octree.OctreeNode> removed, ref NativeList<Octree.OctreeNode> all, ref NativeList<BitField32> neighbourMasks) => {
                foreach (var item in removed) {
                    if (chunks.TryGetValue(item, out TerrainChunk chunk)) {
                        PoolChunk(chunk.gameObject);
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
                }
            };

            readback.onReadback += (TerrainChunk chunk, bool skipped) => {
                if (skipped) {
                    chunk.gameObject.SetActive(false);
                } else {
                    mesher.GenerateMesh(chunk);
                }
            };

            mesher.onMeshingComplete += (TerrainChunk chunk, Meshing.MeshJobHandler.Stats stats) => {
                /*
                if (mesh.VertexCount > 0 && mesh.TriangleCount > 0) {
                    if (chunk.node.depth == octree.maxDepth)
                        collisions.GenerateCollisions(chunk, mesh);

                    chunk.completedFlags |= TerrainChunk.CompletedFlags.Mesh;
                    pendingChunksToShow.Add(chunk.gameObject);
                }

                pendingValidChunks--;
                */
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
                Mesh mesh = new Mesh();
                TerrainChunk component = chunkGo.GetComponent<TerrainChunk>();

                component.voxels = new VoxelData(Allocator.Persistent);
                component.skipIfEmpty = true;

                component.sharedMesh = mesh;

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
    }
}