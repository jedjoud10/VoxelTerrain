using System;
using System.Collections.Generic;
using UnityEngine;

public class PropertyInjector {
    public List<Action<ComputeShader, Dictionary<string, ExecutorTexture>>> injected;

    public PropertyInjector() {
        this.injected = new List<Action<ComputeShader, Dictionary<string, ExecutorTexture>>>();
    }


    public void UpdateInjected(ComputeShader shader, Dictionary<string, ExecutorTexture> textures) {
        foreach (var item in injected) {
            item.Invoke(shader, textures);
        }
    }
}