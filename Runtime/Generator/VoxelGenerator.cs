using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Collections;

using Unity.Collections;




#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jedjoud.VoxelTerrain.Generation {

    // A voxel graph is the base class to inherit from to be able to write custom voxel stuff
    public abstract partial class VoxelGenerator : VoxelBehaviour {
        [Header("Compilation")]
        public bool debugName = true;
        public bool autoCompile = true;

        // Every time the user updates a field, we will re-transpile (to check for hash-differences) and re-compile if needed
        // Also executing the shader at the specified size as well
        private void OnValidate() {

            if (!gameObject.activeSelf)
                return;

            ComputeSecondarySeeds();
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
                ExecuteShader(visualizer.size, visualizer.offset, visualizer.scale, false, true);
                RenderTexture density = (RenderTexture)textures["voxels"];
                RenderTexture colors = (RenderTexture)textures["colors"];
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
            public Variable<float> metallic;
            public Variable<float> smoothness;
        }

        // Execute the voxel graph at a specific position and fetch the density and material values
        public abstract void Execute(Variable<float3> position, out Variable<float> density, out Variable<float3> color);

        // Even lower execution function that allows you to override metallic and smoothness values (and even probably pass your own uv values if needed)
        public virtual void ExecuteWithEverything(AllInputs input, out AllOutputs output) {
            output = new AllOutputs();
            output.metallic = 0.0f;
            output.smoothness = 0.0f;
            Execute(input.position, out output.density, out output.color);
        }

        public override void CallerStart() {
            InitializeReadbackBuffers();
        }

        public override void CallerDispose() {
            DisposeIntermediateTextures();
            DisposeReadbackBuffers();
        }
    }
}