using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Collections;

using Unity.Collections;
using jedjoud.VoxelTerrain.Props;





#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jedjoud.VoxelTerrain.Generation {

    // A voxel graph is the base class to inherit from to be able to write custom voxel stuff
    public abstract partial class VoxelGraph : VoxelBehaviour {
        [Header("Compilation")]
        public bool debugName = true;
        public bool autoCompile = true;

        // Every time the user updates a field, we will re-transpile (to check for hash-differences) and re-compile if needed
        // Also executing the shader at the specified size as well
        private void OnValidate() {

            if (!gameObject.activeSelf)
                return;

            //ComputeSecondarySeeds();
            SoftRecompile();
            OnPropertiesChanged();
            /*
            */
        }

        // Called when the voxel graph's properties get modified
        public void OnPropertiesChanged() {
            if (!gameObject.activeSelf)
                return;

#if UNITY_EDITOR
            var visualizer = GetComponent<VoxelPreview>();
            if (visualizer != null && visualizer.isActiveAndEnabled) {
                var exec = GetComponent<VoxelExecutor>();
                exec.ExecuteShader(visualizer.size, 0, visualizer.offset, visualizer.scale, false, true);
                RenderTexture density = (RenderTexture)exec.textures["voxels"];
                RenderTexture colors = (RenderTexture)exec.textures["colors"];
                visualizer.Meshify(density, colors);
            }
#endif
        }

        public class AllInputs {
            public Variable<float3> position;
        }

        public class AllOutputs {
            public Variable<float> density;
            public Variable<float3> color;
            public Variable<Prop> prop;
        }

        public abstract void Execute(AllInputs input, out AllOutputs output);
    }
}