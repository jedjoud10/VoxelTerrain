using System.IO;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class VoxelGenerator : VoxelBehaviour {
    public ComputeShader shader;

    // Checks if we need to recompile the shader by checking the hash changes.
    // If the context hash changed, then we will recompile the shader
    public void SoftRecompile() {
        if (!gameObject.activeSelf)
            return;
        
        Debug.Log("Soft Recompile...");
        ParsedTranspilation();
        Debug.Log(ctx == null);
        Debug.Log("New: " + ctx.hashinator.hash);
        Debug.Log("Old: " + hash);
        if (hash != ctx.hashinator.hash) {
            Debug.Log("Hash changed, resetting...");
            hash = ctx.hashinator.hash;
            textures = null;

            if (autoCompile) {
                Debug.Log("Hash changed, recompiling...");
                Compile(false);
            }
        }
    }


    // Writes the transpiled shader code to a file and recompiles it automatically (through AssetDatabase)
    public void Compile(bool force) {
        Debug.Log("Compile...");
#if UNITY_EDITOR
        textures = null;
        if (force) {
            ctx = null;
        }

        Debug.Log(ctx == null);
        string source = Transpile();

        if (!AssetDatabase.IsValidFolder("Assets/Voxel Terrain/Compute/")) {
            // TODO: Use package cache instead? would it work???
            AssetDatabase.CreateFolder("Assets", "Voxel Terrain");
            AssetDatabase.CreateFolder("Assets/Voxel Terrain", "Compute");
            Debug.Log("Creating converted compute shaders folders");
        }

        string filePath = "Assets/Voxel Terrain/Compute/" + name.ToLower() + ".compute";
        using (StreamWriter sw = File.CreateText(filePath)) {
            sw.Write(source);
        }

        AssetDatabase.ImportAsset(filePath);
        shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(filePath);
        EditorUtility.SetDirty(shader);
        AssetDatabase.SaveAssetIfDirty(shader);
        shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(filePath);
        EditorUtility.SetDirty(this);

        if (!gameObject.activeSelf)
            return;

        var visualizer = GetComponent<VoxelPreview>();
        visualizer?.InitializeForSize();
#else
            Debug.LogError("Cannot transpile code at runtime");
#endif
    }

#if UNITY_EDITOR
    // Recompiles the graph every time we reload the domain
    [InitializeOnLoadMethod]
    static void RecompileOnDomainReload() {
        VoxelGenerator[] graph = Object.FindObjectsByType<VoxelGenerator>(FindObjectsSortMode.None);

        /*
        foreach (var item in graph) {
            //item.Compile();
        }
        */
    }
#endif
}