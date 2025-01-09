using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

public class VoxelTerrain : MonoBehaviour {
    public GameObject chunkPrefab;
    private List<GameObject> totalChunks;
    private List<GameObject> pooledChunkGameObjects;
    public static VoxelTerrain Instance { get; private set; }
    private Dictionary<Type, VoxelBehaviour> behaviours;

    public void Start() {
        Instance = this;
        behaviours = new Dictionary<Type, VoxelBehaviour>();
        List<VoxelBehaviour> list = GetComponents<VoxelBehaviour>().ToList();
        list.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        foreach (VoxelBehaviour v in list) {
            v.InitWith(this);
            behaviours.Add(v.GetType(), v);
        }
    }

    public void OnApplicationQuit() {
        foreach (var item in behaviours) {
            item.Value.Dispose();
        }
    }

    // Fetches a pooled chunk, or creates a new one from scratch
    public VoxelChunk FetchPooledChunk(VoxelContainer container, Vector3 position, float scale) {
        VoxelChunk chunk;

        if (pooledChunkGameObjects.Count == 0) {
            GameObject obj = Instantiate(chunkPrefab, transform);
            obj.name = $"Voxel Chunk";

            Mesh mesh = new Mesh();
            chunk = obj.GetComponent<VoxelChunk>();
            chunk.sharedMesh = mesh;
        } else {
            GameObject temp = pooledChunkGameObjects[pooledChunkGameObjects.Count - 1];
            pooledChunkGameObjects.RemoveAt(pooledChunkGameObjects.Count - 1);
            temp.GetComponent<MeshCollider>().sharedMesh = null;
            temp.GetComponent<MeshFilter>().sharedMesh = null;
            chunk = temp.GetComponent<VoxelChunk>();
        }

        GameObject chunkGameObject = chunk.gameObject;
        chunkGameObject.transform.position = position;
        chunkGameObject.transform.localScale = scale * Vector3.one;
        chunk.container = container;
        totalChunks.Add(chunkGameObject);
        return chunk;
    }

    // Gives back a pooled chunk for future pooling
    public void PoolChunkBack(VoxelChunk voxelChunk) {
        voxelChunk.gameObject.SetActive(false);
        pooledChunkGameObjects.Add(voxelChunk.gameObject);
        voxelChunk.container?.Dispose();
        voxelChunk.container = null;
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