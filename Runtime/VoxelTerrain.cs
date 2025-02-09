using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class VoxelTerrain : MonoBehaviour {
    public GameObject chunkPrefab;

    [HideInInspector]
    public List<GameObject> totalChunks;
    public static VoxelTerrain Instance { get; private set; }

    [HideInInspector]
    public VoxelCollisions collisions;

    [HideInInspector]
    public VoxelGridSpawner spawner;

    [HideInInspector]
    public VoxelMesher mesher;

    [HideInInspector]
    public VoxelGenerator generator;

    [HideInInspector]
    public VoxelEdits edits;

    public delegate void OnCompleted();
    public event OnCompleted onComplete;
    private int pendingChunks;
    private bool complete;

    public void Start() {
        void Init(VoxelBehaviour val){
            val.terrain = this;
            val.CallerStart();
        }

        complete = false;
        Instance = this;
        totalChunks = new List<GameObject>();

        collisions = GetComponent<VoxelCollisions>();
        spawner = GetComponent<VoxelGridSpawner>();
        mesher = GetComponent<VoxelMesher>();
        generator = GetComponent<VoxelGenerator>();
        edits = GetComponent<VoxelEdits>();

        spawner.onChunkSpawned += (VoxelChunk chunk) => {
            generator.GenerateVoxels(chunk);
            pendingChunks++;
        };

        generator.onReadbackSuccessful += (VoxelChunk chunk) => mesher.GenerateMesh(chunk, false);

        mesher.onVoxelMeshingComplete += (VoxelChunk chunk, VoxelMesh mesh) => collisions.GenerateCollisions(chunk, mesh);

        collisions.onCollisionBakingComplete += (VoxelChunk chunk) => {
            pendingChunks--;
            complete = true;
        };

        onComplete += () => {
            edits.ApplyVoxelEdit(new AddVoxelEdit() {
                center = Vector3.zero,
                strength = -100f,
                writeMaterial = true,
                material = 0,
                radius = 100,
                scale = Vector3.one,
            }, false);
        };

        Init(mesher);
        Init(collisions);
        Init(generator);
        Init(spawner);
        Init(edits);
    }

    public void Update() {
        generator.CallerUpdate();
        mesher.CallerUpdate();
        collisions.CallerUpdate();
        edits.CallerUpdate();

        if (complete && pendingChunks == 0) {
            onComplete?.Invoke();
            complete = false;
        }
    }

    public void OnApplicationQuit() {
        mesher.CallerDispose();
        generator.CallerDispose();
        edits.CallerDispose();

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