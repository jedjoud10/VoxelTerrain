using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

public class VoxelTerrain : MonoBehaviour {
    public GameObject chunkPrefab;
    private List<GameObject> totalChunks;
    public static VoxelTerrain Instance { get; private set; }
    private Dictionary<Type, VoxelBehaviour> behaviours;

    public void Start() {
        Instance = this;
        behaviours = new Dictionary<Type, VoxelBehaviour>();
        totalChunks = new List<GameObject>();
        List<VoxelBehaviour> list = GetComponents<VoxelBehaviour>().ToList();
        list.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        foreach (VoxelBehaviour v in list) {
            v.terrain = this;
            v.Init();
            behaviours.Add(v.GetType(), v);
        }

        foreach (VoxelBehaviour v in list) {
            v.LateInit();
        }
    }

    public void OnApplicationQuit() {
        foreach (var item in behaviours) {
            item.Value.Dispose();
        }

        foreach (var item in totalChunks) {
            item.GetComponent<VoxelChunk>().container.Dispose();
        }
    }

    // Instantiates a new chunk and returns it
    public VoxelChunk FetchChunk(VoxelContainer container, Vector3 position, float scale) {
        VoxelChunk chunk;

        GameObject obj = Instantiate(chunkPrefab, transform);
        obj.name = $"Voxel Chunk";

        Mesh mesh = new Mesh();
        chunk = obj.GetComponent<VoxelChunk>();
        chunk.sharedMesh = mesh;
        
        GameObject chunkGameObject = chunk.gameObject;
        chunkGameObject.transform.position = position;
        chunkGameObject.transform.localScale = scale * Vector3.one;
        chunk.container = container;
        totalChunks.Add(chunkGameObject);
        return chunk;
    }

    // Get the value of a singular voxel at a world point
    public bool TryGetVoxel(Vector3 position, out Voxel voxel) {
        throw new NotImplementedException();
    }


    public T GetBehaviour<T>() where T: VoxelBehaviour {
        return behaviours[typeof(T)] as T;
    }
        
    public bool HasBehaviour<T>() where T : VoxelBehaviour {
        return behaviours.ContainsKey(typeof(T));
    }

    public bool TryGetBehaviour<T>(out T behaviour) where T : VoxelBehaviour {
        if (behaviours.TryGetValue(typeof(T), out VoxelBehaviour value)) {
            behaviour = value as T;
            return true;
        }

        behaviour = null;
        return false;
    }
}