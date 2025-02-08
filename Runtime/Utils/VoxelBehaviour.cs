using UnityEditor;
using UnityEngine;

// Used internally by the classes that handle terrain
public class VoxelBehaviour : MonoBehaviour {
    public virtual int Priority { get; }


    // Fetch the parent terrain heheheha
    [HideInInspector]
    public VoxelTerrain terrain;

    // Initialize the voxel behaviour (called from the voxel terrain)
    public virtual void Init() { }

    // Called after all other voxel behaviours have been initialized
    public virtual void LateInit() { }

    // Dispose of any internally stored memory
    public virtual void Dispose() { }
}