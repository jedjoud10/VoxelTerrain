using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace jedjoud.VoxelTerrain.Generation {
    public class PropertyInjector {
        public List<Action<CommandBuffer, ComputeShader, Dictionary<string, ExecutorTexture>>> injected;

        public PropertyInjector() {
            this.injected = new List<Action<CommandBuffer, ComputeShader, Dictionary<string, ExecutorTexture>>>();
        }


        public void UpdateInjected(CommandBuffer cmds, ComputeShader shader, Dictionary<string, ExecutorTexture> textures) {
            foreach (var item in injected) {
                item.Invoke(cmds, shader, textures);
            }
        }
    }
}