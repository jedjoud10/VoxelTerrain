using System.IO;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jedjoud.VoxelTerrain.Generation {
    public partial class VoxelGenerator : VoxelBehaviour {
        public ComputeShader shader;

        // Checks if we need to recompile the shader by checking the hash changes.
        // If the context hash changed, then we will recompile the shader
        public void SoftRecompile() {
            if (!gameObject.activeSelf)
                return;

            ParsedTranspilation();
            if (hash != ctx.hashinator.hash) {
                hash = ctx.hashinator.hash;
                textures = null;

                if (autoCompile) {
                    Compile(false);
                }
            }
        }


        // Writes the transpiled shader code to a file and recompiles it automatically (through AssetDatabase)
        public void Compile(bool force) {
#if UNITY_EDITOR
            textures = null;
            if (force) {
                ctx = null;
            }

            string source = Transpile();

            if (!AssetDatabase.IsValidFolder("Assets/Voxel Terrain/Compute/")) {
                // TODO: Use package cache instead? would it work???
                AssetDatabase.CreateFolder("Assets", "Voxel Terrain");
                AssetDatabase.CreateFolder("Assets/Voxel Terrain", "Compute");
            }

            string filePath = "Assets/Voxel Terrain/Compute/" + name.ToLower() + ".compute";
            using (StreamWriter sw = File.CreateText(filePath)) {
                sw.Write(source);
            }

            AssetDatabase.ImportAsset(filePath);
            shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(filePath);

            if (shader == null)
                return;

            EditorUtility.SetDirty(shader);
            AssetDatabase.SaveAssetIfDirty(shader);
            AssetDatabase.SaveAssets();
            //AssetDatabase.SaveAssetIfDirty(this);
            if (!gameObject.activeSelf)
                return;
            // amongus

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
}