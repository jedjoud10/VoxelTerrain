using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class VoxelTerrain : MonoBehaviour {
    public GameObject chunkPrefab;
    private List<GameObject> totalChunks;
    public static VoxelTerrain Instance { get; private set; }

    [HideInInspector]
    public VoxelCollisions collisions;

    [HideInInspector]
    public VoxelGridSpawner spawner;

    [HideInInspector]
    public VoxelMesher mesher;

    [HideInInspector]
    public VoxelGenerator generator;

    public delegate void OnCompleted();
    public event OnCompleted onComplete;
    private int pendingChunks;

    public void Start() {
        void Init(VoxelBehaviour val){
            val.terrain = this;
            val.CallerStart();
        }

        Instance = this;
        totalChunks = new List<GameObject>();

        collisions = GetComponent<VoxelCollisions>();
        spawner = GetComponent<VoxelGridSpawner>();
        mesher = GetComponent<VoxelMesher>();
        generator = GetComponent<VoxelGenerator>();

        spawner.onChunkSpawned += (VoxelChunk chunk) => {
            generator.GenerateVoxels(chunk);
            pendingChunks++;
        };

        generator.onReadbackSuccessful += (VoxelChunk chunk) => mesher.GenerateMesh(chunk, true);

        mesher.onVoxelMeshingComplete += (VoxelChunk chunk, VoxelMesh mesh) => collisions.GenerateCollisions(chunk, mesh);

        collisions.onCollisionBakingComplete += (VoxelChunk chunk) => {
            pendingChunks--;

            if (pendingChunks == 0) {
                onComplete?.Invoke();
            }
        };

        Init(mesher);
        Init(collisions);
        Init(generator);
        Init(spawner);
    }

    public void Update() {
        generator.CallerUpdate();
        mesher.CallerUpdate();
        collisions.CallerUpdate();
    }

    public void OnApplicationQuit() {
        mesher.CallerDispose();
        generator.CallerDispose();

        foreach (var item in totalChunks) {
            item.GetComponent<VoxelChunk>().voxels.Dispose();
        }
    }

    // Instantiates a new chunk and returns it
    public VoxelChunk FetchChunk(Vector3 position, float scale) {
        VoxelChunk chunk;

        GameObject obj = Instantiate(chunkPrefab, transform);
        obj.name = $"Voxel Chunk";

        Mesh mesh = new Mesh();
        chunk = obj.GetComponent<VoxelChunk>();
        chunk.sharedMesh = mesh;
        
        GameObject chunkGameObject = chunk.gameObject;
        chunkGameObject.transform.position = position;
        chunkGameObject.transform.localScale = scale * Vector3.one;
        chunk.voxels = new NativeArray<Voxel>(VoxelUtils.Volume, Allocator.Persistent);
        totalChunks.Add(chunkGameObject);
        return chunk;
    }
}