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
        private void OnValidate() {
            GetComponent<VoxelCompiler>().SoftRecompile();
            GetComponent<VoxelCompiler>().OnPropertiesChanged();
        }

        public class AllInputs {
            public Variable<float3> position;
            public Variable<int3> id;
        }

        public class AllOutputs {
            public Variable<float> density;
            public Variable<GpuProp> prop;
            public Variable<int> material;
        }

        public abstract void Execute(AllInputs input, out AllOutputs output);
    }
}